import {
  BadRequestException,
  ConflictException,
  ForbiddenException,
  Injectable,
  NotFoundException,
} from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import * as crypto from 'crypto';
import { Model, Types } from 'mongoose';
import { Company, CompanyDocument } from '../companies/schemas/company.schema';
import {
  Department,
  DepartmentDocument,
} from '../departments/schemas/department.schema';
import { User, UserDocument } from '../users/schemas/user.schema';
import { AcceptInviteDto } from './dto/accept-invite.dto';
import { InviteMemberDto } from './dto/invite-member.dto';
import { UpdateMemberDto } from './dto/update-member.dto';
import {
  CompanyMember,
  CompanyMemberDocument,
  MemberRole,
  MemberStatus,
} from './schemas/company-member.schema';

@Injectable()
export class CompanyMembersService {
  constructor(
    @InjectModel(CompanyMember.name)
    private readonly memberModel: Model<CompanyMemberDocument>,
    @InjectModel(Company.name)
    private readonly companyModel: Model<CompanyDocument>,
    @InjectModel(User.name)
    private readonly userModel: Model<UserDocument>,
    @InjectModel(Department.name)
    private readonly departmentModel: Model<DepartmentDocument>,
  ) {}

  // 1. Mời nhân viên vào công ty
  async inviteMember(
    companyId: string,
    inviterId: string,
    dto: InviteMemberDto,
  ) {
    this.validateObjectId(companyId, 'companyId');

    const company = await this.companyModel.findById(companyId);
    if (!company) {
      throw new NotFoundException('Công ty không tồn tại');
    }

    const email = dto.email.toLowerCase().trim();

    const existingMember = await this.memberModel.findOne({
      companyId: new Types.ObjectId(companyId),
      email,
    });

    if (existingMember && existingMember.status === MemberStatus.ACTIVE) {
      throw new ConflictException('Người dùng này đã là thành viên chính thức của công ty');
    }

    if (dto.departmentId) {
      this.validateObjectId(dto.departmentId, 'departmentId');
      const dept = await this.departmentModel.findOne({
        _id: new Types.ObjectId(dto.departmentId),
        companyId: new Types.ObjectId(companyId),
      });
      if (!dept) {
        throw new NotFoundException('Phòng ban không tồn tại trong công ty này');
      }
    }

    const existingUser = await this.userModel.findOne({ email });

    const inviteToken = crypto.randomBytes(32).toString('hex');
    const inviteExpiresAt = new Date();
    inviteExpiresAt.setDate(inviteExpiresAt.getDate() + 7); // 7 ngày

    let member: CompanyMemberDocument;

    if (existingMember) {
      existingMember.role = dto.role || MemberRole.MEMBER;
      existingMember.departmentId = dto.departmentId
        ? new Types.ObjectId(dto.departmentId)
        : undefined;
      existingMember.inviteToken = inviteToken;
      existingMember.inviteExpiresAt = inviteExpiresAt;
      existingMember.invitedBy = new Types.ObjectId(inviterId);
      existingMember.status = MemberStatus.INVITED;
      member = await existingMember.save();
    } else {
      member = await this.memberModel.create({
        companyId: new Types.ObjectId(companyId),
        userId: existingUser ? existingUser._id : null,
        email,
        departmentId: dto.departmentId
          ? new Types.ObjectId(dto.departmentId)
          : null,
        role: dto.role || MemberRole.MEMBER,
        status: MemberStatus.INVITED,
        inviteToken,
        inviteExpiresAt,
        invitedBy: new Types.ObjectId(inviterId),
      });
    }

    return {
      message: 'Đã gửi lời mời tham gia công ty',
      member,
      inviteLink: `http://localhost:3000/auth/invite?token=${inviteToken}`,
    };
  }

  // 2. Chấp nhận lời mời tham gia công ty
  async acceptInvite(userId: string, userEmail: string, dto: AcceptInviteDto) {
    const member = await this.memberModel
      .findOne({ inviteToken: dto.inviteToken })
      .populate('companyId', 'name slug logo');

    if (!member) {
      throw new NotFoundException('Lời mời không hợp lệ hoặc đã được sử dụng');
    }

    if (member.inviteExpiresAt && member.inviteExpiresAt < new Date()) {
      throw new BadRequestException('Lời mời này đã hết hạn');
    }

    if (member.email.toLowerCase() !== userEmail.toLowerCase()) {
      throw new ForbiddenException(
        `Lời mời này dành cho email ${member.email}, không phải tài khoản hiện tại (${userEmail})`,
      );
    }

    member.userId = new Types.ObjectId(userId);
    member.status = MemberStatus.ACTIVE;
    member.inviteToken = undefined;
    member.inviteExpiresAt = undefined;
    await member.save();

    return {
      message: 'Bạn đã gia nhập công ty thành công!',
      company: member.companyId,
      role: member.role,
    };
  }

  // 3. Lấy danh sách thành viên trong công ty
  async getMembersByCompany(companyId: string) {
    this.validateObjectId(companyId, 'companyId');

    return this.memberModel
      .find({
        companyId: new Types.ObjectId(companyId),
      })
      .populate('userId', 'name email avatar phone')
      .populate('departmentId', 'name code')
      .populate('invitedBy', 'name email')
      .sort({ createdAt: -1 })
      .exec();
  }

  // 4. Cập nhật quyền / phòng ban của thành viên
  async updateMember(memberId: string, dto: UpdateMemberDto) {
    this.validateObjectId(memberId, 'memberId');

    const member = await this.memberModel.findById(memberId);
    if (!member) {
      throw new NotFoundException('Không tìm thấy thành viên này');
    }

    if (dto.departmentId) {
      this.validateObjectId(dto.departmentId, 'departmentId');
      const dept = await this.departmentModel.findOne({
        _id: new Types.ObjectId(dto.departmentId),
        companyId: member.companyId,
      });
      if (!dept) {
        throw new NotFoundException('Phòng ban không tồn tại trong công ty này');
      }
    }

    const payload: any = { ...dto };
    if (dto.departmentId !== undefined) {
      payload.departmentId = dto.departmentId
        ? new Types.ObjectId(dto.departmentId)
        : null;
    }

    return this.memberModel
      .findByIdAndUpdate(memberId, { $set: payload }, { new: true })
      .populate('userId', 'name email avatar')
      .populate('departmentId', 'name code')
      .exec();
  }

  // 5. Xóa / Thu hồi tư cách thành viên
  async removeMember(memberId: string) {
    this.validateObjectId(memberId, 'memberId');

    const member = await this.memberModel.findById(memberId);
    if (!member) {
      throw new NotFoundException('Không tìm thấy thành viên này');
    }

    if (member.role === MemberRole.OWNER) {
      throw new BadRequestException('Không thể xóa người sở hữu (OWNER) của công ty');
    }

    await this.memberModel.findByIdAndDelete(memberId).exec();
    return { message: 'Đã xóa thành viên khỏi công ty thành công' };
  }

  private validateObjectId(id: string, fieldName = 'ID') {
    if (!Types.ObjectId.isValid(id)) {
      throw new BadRequestException(`${fieldName} '${id}' không phải là ObjectId hợp lệ`);
    }
  }
}
