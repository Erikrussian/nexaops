import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { Company, CompanySchema } from '../companies/schemas/company.schema';
import {
  Department,
  DepartmentSchema,
} from '../departments/schemas/department.schema';
import { User, UserSchema } from '../users/schemas/user.schema';
import { CompanyMembersController } from './company-members.controller';
import { CompanyMembersService } from './company-members.service';
import {
  CompanyMember,
  CompanyMemberSchema,
} from './schemas/company-member.schema';

@Module({
  imports: [
    MongooseModule.forFeature([
      { name: CompanyMember.name, schema: CompanyMemberSchema },
      { name: Company.name, schema: CompanySchema },
      { name: User.name, schema: UserSchema },
      { name: Department.name, schema: DepartmentSchema },
    ]),
  ],
  controllers: [CompanyMembersController],
  providers: [CompanyMembersService],
  exports: [CompanyMembersService, MongooseModule],
})
export class CompanyMembersModule {}
