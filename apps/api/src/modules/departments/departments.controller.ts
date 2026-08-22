import {
  Body,
  Controller,
  Delete,
  Get,
  HttpCode,
  HttpStatus,
  Param,
  Patch,
  Post,
  Query,
  UseGuards,
} from '@nestjs/common';
import { JwtAuthGuard } from '../../common/guards/jwt-auth.guard';
import { DepartmentsService } from './departments.service';
import { CreateDepartmentDto } from './dto/create-department.dto';
import { UpdateDepartmentDto } from './dto/update-department.dto';

@Controller()
@UseGuards(JwtAuthGuard)
export class DepartmentsController {
  constructor(private readonly departmentsService: DepartmentsService) {}

  // POST /companies/:companyId/departments
  @Post('companies/:companyId/departments')
  @HttpCode(HttpStatus.CREATED)
  create(
    @Param('companyId') companyId: string,
    @Body() createDepartmentDto: CreateDepartmentDto,
  ) {
    return this.departmentsService.create(companyId, createDepartmentDto);
  }

  // GET /companies/:companyId/departments?tree=true
  @Get('companies/:companyId/departments')
  findAllByCompany(
    @Param('companyId') companyId: string,
    @Query('tree') tree?: string,
  ) {
    const isTree = tree === 'true' || tree === '1';
    return this.departmentsService.findAllByCompany(companyId, isTree);
  }

  // GET /departments/:id
  @Get('departments/:id')
  findById(@Param('id') id: string) {
    return this.departmentsService.findById(id);
  }

  // PATCH /departments/:id
  @Patch('departments/:id')
  update(
    @Param('id') id: string,
    @Body() updateDepartmentDto: UpdateDepartmentDto,
  ) {
    return this.departmentsService.update(id, updateDepartmentDto);
  }

  // DELETE /departments/:id
  @Delete('departments/:id')
  remove(@Param('id') id: string) {
    return this.departmentsService.remove(id);
  }
}
