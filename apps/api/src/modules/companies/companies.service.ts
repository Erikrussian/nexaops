import {
  BadRequestException,
  ConflictException,
  ForbiddenException,
  Injectable,
  NotFoundException,
} from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model, Types } from 'mongoose';
import { CreateCompanyDto } from './dto/create-company.dto';
import { UpdateCompanyDto } from './dto/update-company.dto';
import { Company, CompanyDocument } from './schemas/company.schema';

@Injectable()
export class CompaniesService {
  constructor(
    @InjectModel(Company.name)
    private readonly companyModel: Model<CompanyDocument>,
  ) {}

  // 1. Tạo mới công ty
  async create(
    userId: string,
    createCompanyDto: CreateCompanyDto,
  ): Promise<CompanyDocument> {
    const slug =
      createCompanyDto.slug?.toLowerCase().trim() ||
      this.generateSlug(createCompanyDto.name);

    const existingCompany = await this.companyModel.findOne({ slug });
    if (existingCompany) {
      throw new ConflictException(`Slug công ty '${slug}' đã được sử dụng`);
    }

    const company = new this.companyModel({
      ...createCompanyDto,
      slug,
      ownerId: new Types.ObjectId(userId),
    });

    return company.save();
  }

  // 2. Lấy danh sách công ty của user hiện tại
  async findMyCompanies(userId: string): Promise<CompanyDocument[]> {
    return this.companyModel
      .find({ ownerId: new Types.ObjectId(userId), isActive: true })
      .sort({ createdAt: -1 })
      .exec();
  }

  // 3. Tìm 1 công ty theo ID
  async findById(id: string): Promise<CompanyDocument> {
    this.validateObjectId(id);

    const company = await this.companyModel.findById(id).exec();
    if (!company) {
      throw new NotFoundException(`Không tìm thấy công ty với ID: ${id}`);
    }

    return company;
  }

  // 4. Tìm công ty theo Slug
  async findBySlug(slug: string): Promise<CompanyDocument> {
    const company = await this.companyModel
      .findOne({ slug: slug.toLowerCase().trim() })
      .exec();
    if (!company) {
      throw new NotFoundException(`Không tìm thấy công ty với slug: ${slug}`);
    }

    return company;
  }

  // 5. Cập nhật thông tin công ty (chỉ owner mới được sửa)
  async update(
    id: string,
    userId: string,
    updateCompanyDto: UpdateCompanyDto,
  ): Promise<CompanyDocument> {
    const company = await this.findById(id);

    if (company.ownerId.toString() !== userId) {
      throw new ForbiddenException(
        'Bạn không có quyền chỉnh sửa thông tin công ty này',
      );
    }

    if (
      updateCompanyDto.slug &&
      updateCompanyDto.slug.toLowerCase().trim() !== company.slug
    ) {
      const newSlug = updateCompanyDto.slug.toLowerCase().trim();
      const existing = await this.companyModel.findOne({ slug: newSlug });
      if (existing) {
        throw new ConflictException(`Slug '${newSlug}' đã được sử dụng`);
      }
      updateCompanyDto.slug = newSlug;
    }

    const updated = await this.companyModel
      .findByIdAndUpdate(id, { $set: updateCompanyDto }, { new: true })
      .exec();

    return updated!;
  }

  // 6. Xóa / Tạm dừng công ty
  async remove(
    id: string,
    userId: string,
  ): Promise<{ message: string }> {
    const company = await this.findById(id);

    if (company.ownerId.toString() !== userId) {
      throw new ForbiddenException('Bạn không có quyền xóa công ty này');
    }

    await this.companyModel.findByIdAndDelete(id).exec();
    return { message: 'Đã xóa công ty thành công' };
  }

  private generateSlug(name: string): string {
    const baseSlug = name
      .toLowerCase()
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '')
      .replace(/[đĐ]/g, 'd')
      .replace(/[^a-z0-9\s-]/g, '')
      .trim()
      .replace(/\s+/g, '-');

    const randomSuffix = Math.floor(1000 + Math.random() * 9000);
    return `${baseSlug}-${randomSuffix}`;
  }

  private validateObjectId(id: string) {
    if (!Types.ObjectId.isValid(id)) {
      throw new BadRequestException(`ID '${id}' không phải là ObjectId hợp lệ`);
    }
  }
}
