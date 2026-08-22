import { ValidationPipe } from '@nestjs/common';
import { NestFactory } from '@nestjs/core';
import { AppModule } from './app.module';
async function bootstrap() {
  const app = await NestFactory.create(AppModule);
  // Tự động validate dữ liệu request theo DTO
  app.useGlobalPipes(
    new ValidationPipe({
      whitelist: true, // Tự động loại bỏ các field thừa không có trong DTO
      forbidNonWhitelisted: true, // Báo lỗi nếu client gửi field không hợp lệ
      transform: true, // Tự động convert kiểu dữ liệu (vd: string -> number)
      stopAtFirstError: true,
    }),
  );
  const port = process.env.PORT ?? 3000;
  await app.listen(port);
  console.log(`🚀 Ứng dụng đang chạy tại: http://localhost:${port}`);
}
bootstrap();
