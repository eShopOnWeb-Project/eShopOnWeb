import logging
import os
import ssl

import uvicorn
from typing import Dict, Any, Optional


logger = logging.getLogger("catalog.server")
logging.basicConfig(level=os.getenv("LOG_LEVEL", "INFO"))


def _decode_subject(cert_path: str) -> str:
    try:
        info = ssl._ssl._test_decode_cert(cert_path)  # type: ignore[attr-defined]
        subject = info.get("subject", [])
        for item in subject:
            for key, value in item:
                if key == "commonName":
                    return value
        return ""
    except Exception as ex:  # pragma: no cover - best effort logging
        logger.debug("unable to decode certificate %s: %s", cert_path, ex)
        return ""


def build_tls_args() -> Optional[Dict[str, Any]]:
    cert_path = os.getenv("TLS_CERT")
    key_path = os.getenv("TLS_KEY")
    ca_path = os.getenv("TLS_CA")

    if not all([cert_path, key_path, ca_path]):
        logger.warning("TLS_CERT/TLS_KEY/TLS_CA not set; starting without mTLS")
        return None

    logger.info(
        "Loaded service cert CN=%s; requiring client certs signed by %s",
        _decode_subject(cert_path) or cert_path,
        ca_path,
    )

    return {
        "ssl_certfile": cert_path,
        "ssl_keyfile": key_path,
        "ssl_ca_certs": ca_path,
        "ssl_cert_reqs": ssl.CERT_REQUIRED,
        "ssl_version": ssl.PROTOCOL_TLS,
    }


def main() -> None:
    port = int(os.getenv("API_PORT", "8000"))
    tls_args = build_tls_args() or {}
    uvicorn.run(
        "app.main:app",
        host="0.0.0.0",
        port=port,
        log_level=os.getenv("UVICORN_LOG_LEVEL", "info"),
        **tls_args,
    )


if __name__ == "__main__":
    main()

