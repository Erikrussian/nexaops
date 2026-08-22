import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { HydratedDocument, Schema as MongooseSchema, Types } from 'mongoose';
import { Company } from '../../companies/schemas/company.schema';
import { Department } from '../../departments/schemas/department.schema';
import { User } from '../../users/schemas/user.schema';

export type CompanyMemberDocument = HydratedDocument<CompanyMember>;

export enum MemberRole {
  OWNER = 'OWNER',
  ADMIN = 'ADMIN',
  MANAGER = 'MANAGER',
  MEMBER = 'MEMBER',
}

export enum MemberStatus {
  ACTIVE = 'ACTIVE',
  INVITED = 'INVITED',
  SUSPENDED = 'SUSPENDED',
}

@Schema({ timestamps: true })
export class CompanyMember {
  @Prop({ type: MongooseSchema.Types.ObjectId, ref: Company.name, required: true })
  companyId: Types.ObjectId;

  @Prop({ type: MongooseSchema.Types.ObjectId, ref: User.name, default: null })
  userId?: Types.ObjectId | null;

  @Prop({ required: true, lowercase: true, trim: true })
  email: string;

  @Prop({
    type: MongooseSchema.Types.ObjectId,
    ref: Department.name,
    default: null,
  })
  departmentId?: Types.ObjectId | null;

  @Prop({ enum: MemberRole, default: MemberRole.MEMBER })
  role: MemberRole;

  @Prop({ enum: MemberStatus, default: MemberStatus.INVITED })
  status: MemberStatus;

  @Prop({ default: null })
  inviteToken?: string | null;

  @Prop({ default: null })
  inviteExpiresAt?: Date | null;

  @Prop({ type: MongooseSchema.Types.ObjectId, ref: User.name, default: null })
  invitedBy?: Types.ObjectId | null;
}

export const CompanyMemberSchema = SchemaFactory.createForClass(CompanyMember);
CompanyMemberSchema.index({ companyId: 1, email: 1 }, { unique: true });
CompanyMemberSchema.index({ companyId: 1, userId: 1 });
CompanyMemberSchema.index({ inviteToken: 1 });
