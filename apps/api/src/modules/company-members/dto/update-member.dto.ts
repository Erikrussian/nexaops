import { IsEnum, IsOptional, IsString } from 'class-validator';
import { MemberRole, MemberStatus } from '../schemas/company-member.schema';

export class UpdateMemberDto {
  @IsOptional()
  @IsEnum(MemberRole, {
    message: 'Role không hợp lệ (OWNER, ADMIN, MANAGER, MEMBER)',
  })
  role?: MemberRole;

  @IsOptional()
  @IsEnum(MemberStatus, {
    message: 'Status không hợp lệ (ACTIVE, INVITED, SUSPENDED)',
  })
  status?: MemberStatus;

  @IsOptional()
  @IsString({ message: 'Department ID phải là chuỗi ObjectId hợp lệ' })
  departmentId?: string;
}
