import { IsEmail, IsEnum, IsNotEmpty, IsOptional, IsString } from 'class-validator';
import { MemberRole } from '../schemas/company-member.schema';

export class InviteMemberDto {
  @IsNotEmpty({ message: 'Email nhân viên không được để trống' })
  @IsEmail({}, { message: 'Email không đúng định dạng' })
  email: string;

  @IsOptional()
  @IsEnum(MemberRole, {
    message: 'Role không hợp lệ (OWNER, ADMIN, MANAGER, MEMBER)',
  })
  role?: MemberRole = MemberRole.MEMBER;

  @IsOptional()
  @IsString({ message: 'Department ID phải là chuỗi ObjectId hợp lệ' })
  departmentId?: string;
}
