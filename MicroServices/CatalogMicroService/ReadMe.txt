-----------
----------- run with docker
docker compose:

docker network create eshop-on-web-net

docker compose build

docker compose up -d

or for combined up:

docker compose up --build -d

docker compose logs -f catalog-api

docker compose down
-----------

-----------
initial setup for new user to run it locally:

Inside the project folder

0. delete venv folder if it exists! and exit venv environment

1. create python venv:
    python -m venv venv

2. activate venv:
    venv\Scripts\activate

3. install dependencies in venv:
    pip install -r requirements.txt

4. set up database with docker:
    docker volume create catalog_data

    docker run -d `
        --name catalog-db-local `
        -e POSTGRES_USER=cataloguser `
        -e POSTGRES_PASSWORD=catalogpass `
        -e POSTGRES_DB=catalogdb `
        -v catalog_data:/var/lib/postgresql/data `
        -p 5432:5432 `
        postgres:16

5. set up .env file with:
    API_PORT=8000
    DATABASE_URL=postgresql+asyncpg://cataloguser:catalogpass@localhost:5432/catalogdb

5. alembic upgrade head

6. start app:
    uvicorn app.main:app --port 8000 --reload
-----------


-----------
install dependencies:

pip install -r requirements.txt
-----------

-----------
Alembic migrations:

alembic revision --autogenerate -m "added new column"
alembic upgrade head
-----------

