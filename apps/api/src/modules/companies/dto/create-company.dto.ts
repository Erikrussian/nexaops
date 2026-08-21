import { IsNotEmpty, IsOptional, IsString, Matches } from 'class-validator';

export class CreateCompanyDto {
  @IsNotEmpty({ message: 'Tên công ty không được để trống' })
  @IsString({ message: 'Tên công ty phải là một chuỗi ký tự' })
  name: string;

  @IsOptional()
  @IsString({ message: 'Slug phải là một chuỗi ký tự' })
  @Matches(/^[a-z0-9-]+$/, {
    message: 'Slug chỉ được chứa chữ cái thường, số và dấu gạch ngang (vd: nexa-ops)',
  })
  slug?: string;

  @IsOptional()
  @IsString({ message: 'Mã công ty phải là chuỗi ký tự' })
  code?: string;

  @IsOptional()
  @IsString({ message: 'Logo phải là đường dẫn URL hợp lệ' })
  logo?: string;

  @IsOptional()
  @IsString({ message: 'Mô tả phải là chuỗi ký tự' })
  description?: string;
}
