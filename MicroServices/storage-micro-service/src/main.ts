import { NestFactory } from '@nestjs/core';
import { AppModule } from './app.module';
import { MicroserviceOptions, Transport } from '@nestjs/microservices';
import { Logger } from '@nestjs/common';

async function bootstrap() {
  const logger = new Logger('Bootstrap');

  logger.log('Starting storage microservice application...');

  const app = await NestFactory.create(AppModule);

  const apiPort = 3000;
  await app.listen(apiPort);
  logger.log(`HTTP API listening on port ${apiPort}`);

  const rabbitmqUri = process.env.RABBITMQ_URI || 'amqp://guest:guest@rabbitmq:5672';
  const queueName = 'catalog_item_stock_queue';

  logger.debug(`Connecting to RabbitMQ at ${rabbitmqUri.replace(/:[^:@]+@/, ':****@')}`);

  const microservice = app.connectMicroservice<MicroserviceOptions>({
    transport: Transport.RMQ,
    options: {
      urls: [rabbitmqUri],
      queue: queueName,
      queueOptions: { durable: true },
    },
  });

  await app.startAllMicroservices();
  logger.log(`RabbitMQ microservice connected to queue: ${queueName}`);
  logger.log('Storage microservice is ready and running');
}
bootstrap().catch((error) => {
  const logger = new Logger('Bootstrap');
  logger.error('Failed to start application', error.stack);
  process.exit(1);
});
