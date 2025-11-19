// main.go
package main

import (
	"io"
	"log"
	"net"
	"net/http"
	"net/http/httputil"
	"net/textproto"
	"net/url"
	"bytes"
	"os"
	"strings"
	"sync"
	"time"
	"crypto/tls"
	"crypto/x509"
	"github.com/golang-jwt/jwt/v5"
	"fmt"
)

var secretKey []byte

// --------- Service & Load Balancing ---------

type BackendService struct {
	Name      string
	Instances []*url.URL
	mu        sync.Mutex
	counter   int
}

func (s *BackendService) Next() *url.URL {
	s.mu.Lock()
	defer s.mu.Unlock()
	if len(s.Instances) == 0 {
		return nil
	}
	u := s.Instances[s.counter%len(s.Instances)]
	s.counter++
	return u
}

// --------- Utilities ---------

func mustParse(raw string) *url.URL {
	u, err := url.Parse(strings.TrimRight(strings.TrimSpace(raw), "/"))
	if err != nil {
		log.Fatalf("invalid backend URL %q: %v", raw, err)
	}
	return u
}

func parseBackendList(envVal string, defaults ...string) []*url.URL {
	var parts []string
	if v := strings.TrimSpace(envVal); v != "" {
		parts = strings.Split(v, ",")
	} else {
		parts = defaults
	}
	var urls []*url.URL
	for _, p := range parts {
		if s := strings.TrimSpace(p); s != "" {
			urls = append(urls, mustParse(s))
		}
	}
	return urls
}

func joinPaths(a, b string) string {
	as := strings.HasSuffix(a, "/")
	bp := strings.HasPrefix(b, "/")
	switch {
	case as && bp:
		return a + b[1:]
	case !as && !bp:
		return a + "/" + b
	default:
		return a + b
	}
}

func dropHopByHop(h http.Header) {
	hh := []string{"Connection", "Proxy-Connection", "Keep-Alive", "Proxy-Authenticate",
		"Proxy-Authorization", "TE", "Trailers", "Transfer-Encoding", "Upgrade"}
	for _, f := range h["Connection"] {
		for _, sf := range strings.Split(f, ",") {
			h.Del(textproto.CanonicalMIMEHeaderKey(strings.TrimSpace(sf)))
		}
	}
	for _, k := range hh {
		h.Del(k)
	}
}

func defaultTransport() http.RoundTripper {
	certPath := strings.TrimSpace(os.Getenv("MTLS_CLIENT_CERT"))
	keyPath := strings.TrimSpace(os.Getenv("MTLS_CLIENT_KEY"))
	caPath := strings.TrimSpace(os.Getenv("MTLS_SERVICE_CA"))

	var tlsConfig *tls.Config

	if certPath != "" && keyPath != "" {
		clientCert, err := tls.LoadX509KeyPair(certPath, keyPath)
		if err != nil {
			log.Fatalf("failed to load client certificate %s / %s: %v", certPath, keyPath, err)
		}

		// populate Leaf so we can log friendly info later
		if len(clientCert.Certificate) > 0 {
			if leaf, err := x509.ParseCertificate(clientCert.Certificate[0]); err == nil {
				clientCert.Leaf = leaf
			}
		}

		tlsConfig = &tls.Config{
			MinVersion:   tls.VersionTLS12,
			Certificates: []tls.Certificate{clientCert},
		}
	}

	if caPath != "" {
		caData, err := os.ReadFile(caPath)
		if err != nil {
			log.Fatalf("failed to read service CA %s: %v", caPath, err)
		}
		pool := x509.NewCertPool()
		if !pool.AppendCertsFromPEM(caData) {
			log.Fatalf("service CA %s contained no certificates", caPath)
		}

		if tlsConfig == nil {
			tlsConfig = &tls.Config{MinVersion: tls.VersionTLS12}
		}
		tlsConfig.RootCAs = pool
	}

	if tlsConfig != nil {
		tlsConfig.VerifyConnection = func(cs tls.ConnectionState) error {
			serverCN := ""
			if len(cs.PeerCertificates) > 0 {
				serverCN = cs.PeerCertificates[0].Subject.CommonName
			}
			clientCN := ""
			if len(tlsConfig.Certificates) > 0 && tlsConfig.Certificates[0].Leaf != nil {
				clientCN = tlsConfig.Certificates[0].Leaf.Subject.CommonName
			}
			log.Printf("mTLS handshake OK (client=%s → server=%s)", clientCN, serverCN)
			return nil
		}
	}

	return &http.Transport{
		Proxy: http.ProxyFromEnvironment,
		DialContext: (&net.Dialer{
			Timeout:   5 * time.Second,
			KeepAlive: 30 * time.Second,
		}).DialContext,
		MaxIdleConns:          200,
		IdleConnTimeout:       90 * time.Second,
		TLSHandshakeTimeout:   5 * time.Second,
		ExpectContinueTimeout: 1 * time.Second,
		TLSClientConfig:      tlsConfig,
	}
}

// -------------- Jwt ----------- Middleware

func jwtMiddleware(next http.Handler) http.Handler {
    return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
        auth := r.Header.Get("Authorization")
        log.Printf("Authorization header: %q", auth)

        if !strings.HasPrefix(auth, "Bearer ") {
            log.Println("Authorization header missing 'Bearer ' prefix")
            http.Error(w, "unauthorized", http.StatusUnauthorized)
            return
        }

        tokenStr := strings.TrimPrefix(auth, "Bearer ")
        log.Printf("Extracted token: %s", tokenStr)

        token, err := jwt.Parse(tokenStr, func(token *jwt.Token) (interface{}, error) {
            if _, ok := token.Method.(*jwt.SigningMethodHMAC); !ok {
                errMsg := fmt.Sprintf("unexpected signing method: %v", token.Header["alg"])
                log.Println(errMsg)
                return nil, fmt.Errorf(errMsg)
            }
            return secretKey, nil
        })

        if err != nil {
            log.Printf("Error parsing token: %v", err)
            http.Error(w, "invalid token", http.StatusUnauthorized)
            return
        }
        if !token.Valid {
            log.Println("Token is invalid")
            http.Error(w, "invalid token", http.StatusUnauthorized)
            return
        }

        claims, ok := token.Claims.(jwt.MapClaims)
        if !ok {
            log.Println("Token claims could not be parsed as jwt.MapClaims")
            http.Error(w, "invalid claims", http.StatusUnauthorized)
            return
        }

        log.Printf("Claims: %+v", claims)

        // Audience check
        audClaim, audExists := claims["aud"]
        if !audExists {
            log.Println("Missing 'aud' claim")
            http.Error(w, "invalid audience", http.StatusUnauthorized)
            return
        }

        validAud := false
        switch v := audClaim.(type) {
        case string:
            validAud = (v == "gateway_api")
        case []interface{}:
            for _, a := range v {
                if s, ok := a.(string); ok && s == "gateway_api" {
                    validAud = true
                    break
                }
            }
        default:
            log.Printf("Unexpected type for 'aud' claim: %T", v)
        }

        if !validAud {
            log.Printf("Invalid audience: %v", audClaim)
            http.Error(w, "invalid audience", http.StatusUnauthorized)
            return
        }

        log.Println("JWT validated successfully, forwarding request to next handler")
        next.ServeHTTP(w, r)
    })
}

// --------- Reverse Proxy Builder ---------

func singleHostReverseProxy(target *url.URL) *httputil.ReverseProxy {
	director := func(req *http.Request) {
		req.URL.Scheme = target.Scheme
		req.URL.Host = target.Host

		req.Header.Set("X-Forwarded-Host", req.Host)
		if req.Header.Get("X-Forwarded-Proto") == "" {
			req.Header.Set("X-Forwarded-Proto", "http")
		}
		req.Host = target.Host
	}
	return &httputil.ReverseProxy{
		Director:  director,
		Transport: defaultTransport(),
		ModifyResponse: func(resp *http.Response) error {
			dropHopByHop(resp.Header)
			return nil
		},
		ErrorHandler: func(w http.ResponseWriter, r *http.Request, err error) {
			log.Printf("proxy error: %v", err)
			http.Error(w, "backend service unavailable", http.StatusBadGateway)
		},
	}
}

// --------- Gateway Logic ---------

func forwardToService(w http.ResponseWriter, r *http.Request, svc *BackendService, strippedPath string) {
	target := svc.Next()
	if target == nil {
		http.Error(w, "no backend available", http.StatusServiceUnavailable)
		return
	}

	outPath := joinPaths(target.Path, strippedPath)
	outQuery := r.URL.RawQuery

	log.Printf("[%s] %s %s -> %s%s%s",
		svc.Name,
		r.Method,
		r.URL.RequestURI(),
		target.Scheme+"://"+target.Host,
		outPath,
		func() string {
			if outQuery != "" {
				return "?" + outQuery
			}
			return ""
		}(),
	)

	r2 := r.Clone(r.Context())
	r2.URL.Path = outPath
	r2.URL.RawQuery = outQuery
	dropHopByHop(r2.Header)
	if r2.Body == nil {
		r2.Body = http.NoBody
	}

	proxy := singleHostReverseProxy(target)
	proxy.ServeHTTP(w, r2)
}

func stripFirstSegment(path, segment string) string {
	if !strings.HasPrefix(path, "/") {
		path = "/" + path
	}
	stripped := strings.TrimPrefix(path, "/"+segment)
	if stripped == "" {
		return "/"
	}
	if !strings.HasPrefix(stripped, "/") {
		stripped = "/" + stripped
	}
	return stripped
}

// --------- CORS Middleware ---------

func withCORS(next http.Handler, allowedOrigin string) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		origin := r.Header.Get("Origin")
		if origin == allowedOrigin {
			w.Header().Set("Access-Control-Allow-Origin", origin)
			w.Header().Set("Vary", "Origin")
			w.Header().Set("Access-Control-Allow-Credentials", "true")
			w.Header().Set("Access-Control-Allow-Headers", "authorization, content-type")
			w.Header().Set("Access-Control-Allow-Methods", "GET,POST,PUT,PATCH,DELETE,OPTIONS")
			w.Header().Set("Access-Control-Expose-Headers", "content-type")
		}
		if r.Method == http.MethodOptions {
			w.WriteHeader(http.StatusNoContent)
			return
		}
		next.ServeHTTP(w, r)
	})
}

// --------- Main ---------

func main() {
	catalogBackends := parseBackendList(
		os.Getenv("CATALOG_BACKENDS"),
		"https://catalog:8000",
	)
	ordersBackends := parseBackendList(
		os.Getenv("ORDERS_BACKENDS"),
		"https://orders:8001",
	)

	services := map[string]*BackendService{
		"catalog": {Name: "catalog", Instances: catalogBackends},
		"orders":  {Name: "orders", Instances: ordersBackends},
	}


	handler := func(serviceName string) http.HandlerFunc {
		return func(w http.ResponseWriter, r *http.Request) {
			svc := services[serviceName]
			if svc == nil {
				http.Error(w, "service not found", http.StatusNotFound)
				return
			}
			stripped := stripFirstSegment(r.URL.Path, serviceName)
			forwardToService(w, r, svc, stripped)
		}
	}

	mux := http.NewServeMux()
	mux.HandleFunc("/catalog/", handler("catalog"))
	mux.HandleFunc("/orders/", handler("orders"))
	mux.HandleFunc("/", func(w http.ResponseWriter, r *http.Request) {
		io.WriteString(w, "gateway: try /catalog/... or /orders/...\n")
	})
	mux.HandleFunc("/healthz", func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("Content-Type", "text/plain")
		w.WriteHeader(http.StatusOK)
		io.WriteString(w, "ok")
	})

	adminOrigin := "http://localhost:5106"
	addr := ":8080"
	log.Printf("API Gateway listening on %s (CORS allowed from %s)", addr, adminOrigin)

	key, err := os.ReadFile("/secrets/jwt/secret.key")
	if err != nil {
	    log.Fatalf("failed to load secret key: %v", err)
	}
	secretKey = bytes.TrimSpace(key)

	securedMux := jwtMiddleware(mux)

	log.Fatal(http.ListenAndServe(addr, withCORS(securedMux, adminOrigin)))
}