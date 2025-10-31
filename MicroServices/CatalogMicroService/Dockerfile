FROM python:3.12-slim

# Install system dependencies
RUN apt-get update && apt-get install -y postgresql-client gcc libpq-dev && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# Copy requirements
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Copy app
COPY . .

# Make wait script executable
RUN chmod +x wait-for-postgres.sh

# Default command (docker-compose can override)
CMD ["sh", "-c", "./wait-for-postgres.sh"]