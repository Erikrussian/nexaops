import { IsNotEmpty, IsString } from 'class-validator';

export class AcceptInviteDto {
  @IsNotEmpty({ message: 'Token lời mời không được để trống' })
  @IsString()
  inviteToken: string;
}
