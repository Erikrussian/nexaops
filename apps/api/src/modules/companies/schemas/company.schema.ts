import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { HydratedDocument, Schema as MongooseSchema, Types } from 'mongoose';
import { User } from '../../users/schemas/user.schema';

export type CompanyDocument = HydratedDocument<Company>;

@Schema({ timestamps: true })
export class Company {
  @Prop({ required: true, trim: true })
  name: string;

  @Prop({ required: true, unique: true, lowercase: true, trim: true })
  slug: string;

  @Prop({ trim: true, default: null })
  code?: string;

  @Prop({ default: null })
  logo?: string;

  @Prop({ default: null, trim: true })
  description?: string;

  @Prop({ type: MongooseSchema.Types.ObjectId, ref: User.name, required: true })
  ownerId: Types.ObjectId;

  @Prop({ default: true })
  isActive: boolean;
}

export const CompanySchema = SchemaFactory.createForClass(Company);
CompanySchema.index({ ownerId: 1 });
