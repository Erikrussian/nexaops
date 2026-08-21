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
  UseGuards,
} from '@nestjs/common';
import { CurrentUser } from '../../common/decorators/current-user.decorator';
import { JwtAuthGuard } from '../../common/guards/jwt-auth.guard';
import { CompaniesService } from './companies.service';
import { CreateCompanyDto } from './dto/create-company.dto';
import { UpdateCompanyDto } from './dto/update-company.dto';

@Controller('companies')
@UseGuards(JwtAuthGuard)
export class CompaniesController {
  constructor(private readonly companiesService: CompaniesService) {}

  // POST /companies (Tạo công ty mới)
  @Post()
  @HttpCode(HttpStatus.CREATED)
  create(
    @CurrentUser('userId') userId: string,
    @Body() createCompanyDto: CreateCompanyDto,
  ) {
    return this.companiesService.create(userId, createCompanyDto);
  }

  // GET /companies (Lấy danh sách các công ty của user hiện tại)
  @Get()
  findMyCompanies(@CurrentUser('userId') userId: string) {
    return this.companiesService.findMyCompanies(userId);
  }

  // GET /companies/slug/:slug
  @Get('slug/:slug')
  findBySlug(@Param('slug') slug: string) {
    return this.companiesService.findBySlug(slug);
  }

  // GET /companies/:id
  @Get(':id')
  findById(@Param('id') id: string) {
    return this.companiesService.findById(id);
  }

  // PATCH /companies/:id
  @Patch(':id')
  update(
    @Param('id') id: string,
    @CurrentUser('userId') userId: string,
    @Body() updateCompanyDto: UpdateCompanyDto,
  ) {
    return this.companiesService.update(id, userId, updateCompanyDto);
  }

  // DELETE /companies/:id
  @Delete(':id')
  remove(@Param('id') id: string, @CurrentUser('userId') userId: string) {
    return this.companiesService.remove(id, userId);
  }
}
