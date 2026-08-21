import { Type } from 'class-transformer';
import { IsInt, IsOptional, IsString, Max, Min } from 'class-validator';

export class QueryUserDto {
  @IsOptional()
  @Type(() => Number)
  @IsInt({ message: 'Page phải là số nguyên' })
  @Min(1, { message: 'Page tối thiểu là 1' })
  page?: number = 1;

  @IsOptional()
  @Type(() => Number)
  @IsInt({ message: 'Limit phải là số nguyên' })
  @Min(1, { message: 'Limit tối thiểu là 1' })
  @Max(100, { message: 'Limit tối đa là 100' })
  limit?: number = 10;

  @IsOptional()
  @IsString()
  search?: string;
}
