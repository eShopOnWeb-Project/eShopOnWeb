import { Entity, Column, PrimaryGeneratedColumn } from 'typeorm';

@Entity('reservation')
export class Reservation {
  @PrimaryGeneratedColumn()
  id: number;

  @Column('int')
  itemId: number;

  @Column('int')
  amount: number;

  @Column('timestamp')
  expiresAt: Date;

  @Column({ default: 'reserved' })
  status: 'reserved' | 'confirmed' | 'cancelled';
}