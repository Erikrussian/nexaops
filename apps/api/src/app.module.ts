import { Module } from '@nestjs/common';
import { ConfigModule, ConfigService } from '@nestjs/config';
import { MongooseModule } from '@nestjs/mongoose';
import { AppController } from './app.controller';
import { AppService } from './app.service';
import { AuthModule } from './modules/auth/auth.module';
import { CompaniesModule } from './modules/companies/companies.module';
import { UsersModule } from './modules/users/users.module';

@Module({
  imports: [
    // 1. Đọc file .env từ thư mục gốc
    ConfigModule.forRoot({
      isGlobal: true,
      envFilePath: '../../.env',
    }),
    // 2. Kết nối tới MongoDB thông qua biến MONGODB_URI
    MongooseModule.forRootAsync({
      imports: [ConfigModule],
      useFactory: async (configService: ConfigService) => ({
        uri: configService.get<string>('MONGODB_URI'),
      }),
      inject: [ConfigService],
    }),
    // 3. Các Feature Modules
    UsersModule,
    AuthModule,
    CompaniesModule,
  ],
  controllers: [AppController],
  providers: [AppService],
})
export class AppModule {}
