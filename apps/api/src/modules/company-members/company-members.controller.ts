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
import { CompanyMembersService } from './company-members.service';
import { AcceptInviteDto } from './dto/accept-invite.dto';
import { InviteMemberDto } from './dto/invite-member.dto';
import { UpdateMemberDto } from './dto/update-member.dto';

@Controller()
@UseGuards(JwtAuthGuard)
export class CompanyMembersController {
  constructor(private readonly membersService: CompanyMembersService) {}

  // POST /companies/:companyId/members/invite (Mời nhân viên)
  @Post('companies/:companyId/members/invite')
  @HttpCode(HttpStatus.CREATED)
  inviteMember(
    @Param('companyId') companyId: string,
    @CurrentUser('userId') inviterId: string,
    @Body() dto: InviteMemberDto,
  ) {
    return this.membersService.inviteMember(companyId, inviterId, dto);
  }

  // POST /company-members/accept-invite (Chấp nhận lời mời)
  @Post('company-members/accept-invite')
  @HttpCode(HttpStatus.OK)
  acceptInvite(
    @CurrentUser('userId') userId: string,
    @CurrentUser('email') userEmail: string,
    @Body() dto: AcceptInviteDto,
  ) {
    return this.membersService.acceptInvite(userId, userEmail, dto);
  }

  // GET /companies/:companyId/members (Danh sách nhân viên)
  @Get('companies/:companyId/members')
  getMembersByCompany(@Param('companyId') companyId: string) {
    return this.membersService.getMembersByCompany(companyId);
  }

  // PATCH /company-members/:memberId (Cập nhật quyền, phòng ban)
  @Patch('company-members/:memberId')
  updateMember(
    @Param('memberId') memberId: string,
    @Body() dto: UpdateMemberDto,
  ) {
    return this.membersService.updateMember(memberId, dto);
  }

  // DELETE /company-members/:memberId (Xóa nhân viên)
  @Delete('company-members/:memberId')
  removeMember(@Param('memberId') memberId: string) {
    return this.membersService.removeMember(memberId);
  }
}
