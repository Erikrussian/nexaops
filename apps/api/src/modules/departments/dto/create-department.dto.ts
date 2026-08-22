import { IsNotEmpty, IsOptional, IsString } from 'class-validator';

export class CreateDepartmentDto {
  @IsNotEmpty({ message: 'Tên phòng ban không được để trống' })
  @IsString({ message: 'Tên phòng ban phải là chuỗi ký tự' })
  name: string;

  @IsOptional()
  @IsString({ message: 'Mã phòng ban phải là chuỗi ký tự' })
  code?: string;

  @IsOptional()
  @IsString({ message: 'Parent ID phải là một chuỗi ObjectId hợp lệ' })
  parentId?: string;

  @IsOptional()
  @IsString({ message: 'Manager ID phải là một chuỗi ObjectId hợp lệ' })
  managerId?: string;

  @IsOptional()
  @IsString({ message: 'Mô tả phải là chuỗi ký tự' })
  description?: string;
}
