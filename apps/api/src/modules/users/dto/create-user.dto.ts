import { IsEmail, IsEnum, IsNotEmpty, IsOptional, IsString, MinLength } from 'class-validator';
import { UserRole } from '../schemas/user.schema';

export class CreateUserDto {
  @IsNotEmpty({ message: 'Tên không được để trống' })
  @IsString({ message: 'Tên phải là một chuỗi ký tự' })
  name: string;

  @IsNotEmpty({ message: 'Email không được để trống' })
  @IsEmail({}, { message: 'Email không đúng định dạng' })
  email: string;

  @IsNotEmpty({ message: 'Mật khẩu không được để trống' })
  @IsString({ message: 'Mật khẩu phải là một chuỗi ký tự' })
  @MinLength(6, { message: 'Mật khẩu phải có ít nhất 6 ký tự' })
  password: string;

  @IsOptional()
  @IsEnum(UserRole, { message: 'Role không hợp lệ (admin hoặc user)' })
  role?: UserRole;

  @IsOptional()
  @IsString({ message: 'Avatar phải là đường dẫn URL hợp lệ' })
  avatar?: string;

  @IsOptional()
  @IsString({ message: 'Số điện thoại không hợp lệ' })
  phone?: string;
}