import {
  BadRequestException,
  ConflictException,
  Injectable,
  NotFoundException,
} from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import * as bcrypt from 'bcryptjs';
import { Model, Types } from 'mongoose';
import { CreateUserDto } from './dto/create-user.dto';
import { QueryUserDto } from './dto/query-user.dto';
import { UpdateUserDto } from './dto/update-user.dto';
import { User, UserDocument } from './schemas/user.schema';

@Injectable()
export class UsersService {
  constructor(
    @InjectModel(User.name) private readonly userModel: Model<UserDocument>,
  ) {}

  // 1. Tạo mới người dùng
  async create(createUserDto: CreateUserDto): Promise<UserDocument> {
    const existingUser = await this.userModel.findOne({
      email: createUserDto.email.toLowerCase(),
    });

    if (existingUser) {
      throw new ConflictException('Email này đã được sử dụng');
    }

    const hashedPassword = await bcrypt.hash(createUserDto.password, 10);

    const newUser = new this.userModel({
      ...createUserDto,
      email: createUserDto.email.toLowerCase(),
      password: hashedPassword,
    });

    return newUser.save();
  }

  // 2. Lấy danh sách người dùng (có phân trang & tìm kiếm)
  async findAll(query: QueryUserDto) {
    const { page = 1, limit = 10, search } = query;
    const filter: Record<string, any> = {};

    if (search) {
      filter.$or = [
        { name: { $regex: search, $options: 'i' } },
        { email: { $regex: search, $options: 'i' } },
      ];
    }

    const skip = (page - 1) * limit;

    const [items, total] = await Promise.all([
      this.userModel
        .find(filter)
        .sort({ createdAt: -1 })
        .skip(skip)
        .limit(limit)
        .exec(),
      this.userModel.countDocuments(filter),
    ]);

    return {
      items,
      meta: {
        total,
        page,
        limit,
        totalPages: Math.ceil(total / limit),
      },
    };
  }

  // 3. Tìm 1 người dùng theo ID
  async findOne(id: string): Promise<UserDocument> {
    this.validateObjectId(id);

    const user = await this.userModel.findById(id).exec();
    if (!user) {
      throw new NotFoundException(`Không tìm thấy người dùng với ID: ${id}`);
    }

    return user;
  }

  // 4. Tìm 1 người dùng theo Email (hỗ trợ kèm password phục vụ Auth sau này)
  async findByEmail(
    email: string,
    selectPassword = false,
  ): Promise<UserDocument | null> {
    const query = this.userModel.findOne({ email: email.toLowerCase() });
    if (selectPassword) {
      query.select('+password');
    }
    return query.exec();
  }

  // 5. Cập nhật thông tin người dùng
  async update(
    id: string,
    updateUserDto: UpdateUserDto,
  ): Promise<UserDocument> {
    this.validateObjectId(id);

    const user = await this.userModel.findById(id);
    if (!user) {
      throw new NotFoundException(`Không tìm thấy người dùng với ID: ${id}`);
    }

    if (
      updateUserDto.email &&
      updateUserDto.email.toLowerCase() !== user.email
    ) {
      const existingUser = await this.userModel.findOne({
        email: updateUserDto.email.toLowerCase(),
      });
      if (existingUser) {
        throw new ConflictException('Email này đã được sử dụng bởi người khác');
      }
      updateUserDto.email = updateUserDto.email.toLowerCase();
    }

    if (updateUserDto.password) {
      updateUserDto.password = await bcrypt.hash(updateUserDto.password, 10);
    }

    const updatedUser = await this.userModel
      .findByIdAndUpdate(id, { $set: updateUserDto }, { new: true })
      .exec();

    return updatedUser!;
  }

  // 6. Xóa người dùng
  async remove(id: string): Promise<{ message: string }> {
    this.validateObjectId(id);

    const result = await this.userModel.findByIdAndDelete(id).exec();
    if (!result) {
      throw new NotFoundException(`Không tìm thấy người dùng với ID: ${id}`);
    }

    return { message: 'Đã xóa người dùng thành công' };
  }

  // Helper kiểm tra tính hợp lệ của MongoDB ObjectId
  private validateObjectId(id: string) {
    if (!Types.ObjectId.isValid(id)) {
      throw new BadRequestException(`ID '${id}' không phải là ObjectId hợp lệ`);
    }
  }
}
