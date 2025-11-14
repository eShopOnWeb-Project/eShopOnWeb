FROM python:3.12-slim

# Install system dependencies
RUN apt-get update && apt-get install -y postgresql-client gcc libpq-dev && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# Copy requirements
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Copy app
COPY . .

CMD ["python", "-m", "app.server"]