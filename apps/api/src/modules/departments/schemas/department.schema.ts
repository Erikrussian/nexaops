import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { HydratedDocument, Schema as MongooseSchema, Types } from 'mongoose';
import { Company } from '../../companies/schemas/company.schema';
import { User } from '../../users/schemas/user.schema';

export type DepartmentDocument = HydratedDocument<Department>;

@Schema({ timestamps: true })
export class Department {
  @Prop({ type: MongooseSchema.Types.ObjectId, ref: Company.name, required: true })
  companyId: Types.ObjectId;

  @Prop({ required: true, trim: true })
  name: string;

  @Prop({ trim: true, default: null })
  code?: string;

  @Prop({ type: MongooseSchema.Types.ObjectId, ref: 'Department', default: null })
  parentId?: Types.ObjectId | null;

  @Prop({ type: MongooseSchema.Types.ObjectId, ref: User.name, default: null })
  managerId?: Types.ObjectId | null;

  @Prop({ default: null, trim: true })
  description?: string;

  @Prop({ default: true })
  isActive: boolean;
}

export const DepartmentSchema = SchemaFactory.createForClass(Department);
DepartmentSchema.index({ companyId: 1, name: 1 });
DepartmentSchema.index({ companyId: 1, parentId: 1 });
