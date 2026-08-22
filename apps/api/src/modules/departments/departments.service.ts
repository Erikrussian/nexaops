import {
  BadRequestException,
  ConflictException,
  Injectable,
  NotFoundException,
} from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model, Types } from 'mongoose';
import { CreateDepartmentDto } from './dto/create-department.dto';
import { UpdateDepartmentDto } from './dto/update-department.dto';
import { Department, DepartmentDocument } from './schemas/department.schema';

export interface DepartmentTreeNode extends Record<string, any> {
  _id: string;
  name: string;
  code?: string;
  parentId?: string | null;
  managerId?: any;
  children: DepartmentTreeNode[];
}

@Injectable()
export class DepartmentsService {
  constructor(
    @InjectModel(Department.name)
    private readonly departmentModel: Model<DepartmentDocument>,
  ) {}

  // 1. Tạo phòng ban mới
  async create(
    companyId: string,
    createDepartmentDto: CreateDepartmentDto,
  ): Promise<DepartmentDocument> {
    this.validateObjectId(companyId, 'companyId');

    const existingName = await this.departmentModel.findOne({
      companyId: new Types.ObjectId(companyId),
      name: createDepartmentDto.name.trim(),
      isActive: true,
    });

    if (existingName) {
      throw new ConflictException(
        `Phòng ban với tên '${createDepartmentDto.name}' đã tồn tại trong công ty`,
      );
    }

    if (createDepartmentDto.parentId) {
      this.validateObjectId(createDepartmentDto.parentId, 'parentId');
      const parent = await this.departmentModel.findOne({
        _id: new Types.ObjectId(createDepartmentDto.parentId),
        companyId: new Types.ObjectId(companyId),
      });
      if (!parent) {
        throw new NotFoundException('Phòng ban cha (parentId) không tồn tại trong công ty này');
      }
    }

    if (createDepartmentDto.managerId) {
      this.validateObjectId(createDepartmentDto.managerId, 'managerId');
    }

    const newDepartment = new this.departmentModel({
      ...createDepartmentDto,
      companyId: new Types.ObjectId(companyId),
      parentId: createDepartmentDto.parentId
        ? new Types.ObjectId(createDepartmentDto.parentId)
        : null,
      managerId: createDepartmentDto.managerId
        ? new Types.ObjectId(createDepartmentDto.managerId)
        : null,
    });

    return newDepartment.save();
  }

  // 2. Lấy danh sách phòng ban (phẳng hoặc cấu trúc cây)
  async findAllByCompany(companyId: string, tree = false) {
    this.validateObjectId(companyId, 'companyId');

    const departments = await this.departmentModel
      .find({
        companyId: new Types.ObjectId(companyId),
        isActive: true,
      })
      .populate('managerId', 'name email avatar')
      .sort({ createdAt: 1 })
      .lean()
      .exec();

    if (!tree) {
      return departments;
    }

    return this.buildDepartmentTree(departments);
  }

  // 3. Tìm 1 phòng ban theo ID
  async findById(id: string): Promise<DepartmentDocument> {
    this.validateObjectId(id, 'id');

    const department = await this.departmentModel
      .findById(id)
      .populate('managerId', 'name email avatar')
      .populate('parentId', 'name code')
      .exec();

    if (!department) {
      throw new NotFoundException(`Không tìm thấy phòng ban với ID: ${id}`);
    }

    return department;
  }

  // 4. Cập nhật thông tin phòng ban
  async update(
    id: string,
    updateDepartmentDto: UpdateDepartmentDto,
  ): Promise<DepartmentDocument> {
    const department = await this.findById(id);

    if (updateDepartmentDto.name && updateDepartmentDto.name !== department.name) {
      const existing = await this.departmentModel.findOne({
        companyId: department.companyId,
        name: updateDepartmentDto.name.trim(),
        _id: { $ne: department._id },
        isActive: true,
      });
      if (existing) {
        throw new ConflictException(
          `Tên phòng ban '${updateDepartmentDto.name}' đã được sử dụng`,
        );
      }
    }

    if (updateDepartmentDto.parentId) {
      if (updateDepartmentDto.parentId === id) {
        throw new BadRequestException('Phòng ban không thể làm cha của chính nó');
      }
      this.validateObjectId(updateDepartmentDto.parentId, 'parentId');
    }

    const payload: any = { ...updateDepartmentDto };
    if (updateDepartmentDto.parentId !== undefined) {
      payload.parentId = updateDepartmentDto.parentId
        ? new Types.ObjectId(updateDepartmentDto.parentId)
        : null;
    }
    if (updateDepartmentDto.managerId !== undefined) {
      payload.managerId = updateDepartmentDto.managerId
        ? new Types.ObjectId(updateDepartmentDto.managerId)
        : null;
    }

    const updated = await this.departmentModel
      .findByIdAndUpdate(id, { $set: payload }, { new: true })
      .populate('managerId', 'name email avatar')
      .exec();

    return updated!;
  }

  // 5. Xóa phòng ban (kiểm tra nếu có phòng ban con thì chặn)
  async remove(id: string): Promise<{ message: string }> {
    await this.findById(id);

    const hasChildren = await this.departmentModel.findOne({
      parentId: new Types.ObjectId(id),
      isActive: true,
    });

    if (hasChildren) {
      throw new BadRequestException(
        'Không thể xóa phòng ban này vì đang có các phòng ban con trực thuộc',
      );
    }

    await this.departmentModel.findByIdAndDelete(id).exec();
    return { message: 'Đã xóa phòng ban thành công' };
  }

  // Helper dựng cây phân cấp Org Chart
  private buildDepartmentTree(departments: any[]): DepartmentTreeNode[] {
    const map = new Map<string, DepartmentTreeNode>();
    const roots: DepartmentTreeNode[] = [];

    departments.forEach((dept) => {
      map.set(dept._id.toString(), {
        ...dept,
        _id: dept._id.toString(),
        children: [],
      });
    });

    departments.forEach((dept) => {
      const node = map.get(dept._id.toString())!;
      if (dept.parentId && map.has(dept.parentId.toString())) {
        map.get(dept.parentId.toString())!.children.push(node);
      } else {
        roots.push(node);
      }
    });

    return roots;
  }

  private validateObjectId(id: string, fieldName = 'ID') {
    if (!Types.ObjectId.isValid(id)) {
      throw new BadRequestException(`${fieldName} '${id}' không phải là ObjectId hợp lệ`);
    }
  }
}
