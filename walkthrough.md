# NexaOps Phase 20 Walkthrough: Secure Authentication Session & Company Workspace Management

Phase 20 hoàn thành kiến trúc **Secure Authentication Session (Phiên Xác Thực Bảo Mật Chuẩn Production)** và **Company Workspace Management (Quản Lý & Chuyển Đổi Không Gian Làm Việc Đa Tổ Chức)**, đảm bảo multi-tenant isolation, loại bỏ hoàn toàn rủi ro refresh token loop và mock fake success.

---

## 1. Kiến Trúc Session Mới & Security Hardening

### A. Refresh Token Rotation & Hashing
- **Access Token Lifecycle**: Giảm thời hạn xuống **15 phút** (short-lived JWT), giảm thiểu rủi ro khi access token bị lộ.
- **Refresh Token Generation**: Sinh bằng bộ tạo số ngẫu nhiên mật mã an toàn (`RandomNumberGenerator`, 64 bytes cryptographic entropy).
- **Hashed Storage**: Chỉ lưu trữ mã băm một chiều **SHA-256** của refresh token trong PostgreSQL (`refresh_tokens.token_hash`), không lưu trữ plaintext.
- **Single-Use Rotation**: Sau mỗi lần gọi `POST /auth/refresh`, refresh token cũ bị thu hồi (`revoked_at = now`) và thay thế bằng refresh token mới (`replaced_by_token_id`).
- **Reuse Detection & Token Family Breach Revocation**: Nếu một refresh token đã bị thu hồi được gửi lên lại (dấu hiệu replay attack hoặc token leak), hệ thống tự động thu hồi **toàn bộ họ session token** (`TokenFamilyId`) của người dùng và ghi nhật ký cảnh báo an ninh.

### B. Cookie & CSRF Strategy
- Refresh token được quản lý tự động qua HTTP cookie:
  - `HttpOnly = true`: Ngăn chặn hoàn toàn truy cập từ JavaScript (chống XSS).
  - `Secure = true`: Bắt buộc truyền qua HTTPS trong môi trường production.
  - `SameSite = SameSiteMode.Lax`: Bảo vệ chống tấn công CSRF cross-site.
  - `Path = /auth`: Giới hạn phạm vi cookie chỉ gửi đến các endpoint xác thực.
- **Zero Web Storage**: Tuyệt đối không lưu refresh token trong `localStorage` hay `sessionStorage`.
- **In-Memory Access Token**: Phía client, access token chỉ tồn tại trong bộ nhớ RAM (`ApiClient` in-memory closure), khôi phục phiên im lặng (silent restore) qua cookie khi reload trang.

### C. Session Management Endpoints
- `POST /auth/refresh`: Cấp mới access token và xoay vòng refresh token.
- `POST /auth/logout`: Thu hồi session hiện tại trên database và xóa cookie.
- `POST /auth/logout-all`: Thu hồi toàn bộ session đang hoạt động của người dùng trên mọi thiết bị.
- `GET /auth/sessions`: Lấy danh sách các session đang hoạt động (IP, User-Agent, thời gian tạo, hết hạn).
- `DELETE /auth/sessions/{sessionId}`: Thu hồi session cụ thể của người dùng (có kiểm tra chống cross-user revocation).

---

## 2. Frontend Session State & Request Interception

- **Silent Restore**: Khởi động ứng dụng kiểm tra phiên qua `api.refreshToken()`. Phân biệt rõ trạng thái `isInitializing` (hiển thị loading shell) và `isAuthenticated`.
- **Mutex Refresh Queue**: Khi access token hết hạn và nhận HTTP `401 Unauthorized`:
  - Request đầu tiên giữ lock làm mới token.
  - Các request song song tiếp theo được xếp vào hàng đợi chờ (`refreshQueue`).
  - Khi refresh hoàn tất, toàn bộ request trong hàng đợi được retry tự động với access token mới.
  - Ngăn ngừa hoàn toàn hiện tượng nhiều request refresh đồng thời gây conflict rotation và vòng lặp vô hạn (infinite refresh loop).
- **Clean Session Eviction**: Khi logout hoặc token family bị thu hồi, tự động xóa sạch in-memory token, user profile và tenant cache (`sessionStorage.removeItem`).

---

## 3. Company Workspace Management & Permission Control

- **CompanyContext (`CompanyContext.tsx`)**:
  - Quản lý tập trung `companies`, `activeCompany`, `userRole`, `selectCompany`.
  - Cô lập dữ liệu: Khi chuyển đổi workspace, toàn bộ dữ liệu tổ chức cũ được xóa lập tức trước khi tải dữ liệu mới, ngăn chặn tuyệt đối tình trạng rò rỉ dữ liệu chéo (cross-tenant data leakage) trong thời gian loading.
  - Xác thực lại quyền hạn khi reload từ danh sách tổ chức được backend cấp phép (`GET /companies/my`).
- **Role Hierarchy & Route Protection**:
  - Phân quyền theo cấp bậc: `OWNER` (4) > `ADMIN` (3) > `MANAGER` (2) > `MEMBER` (1).
  - Tab `audit` (Nhật ký kiểm toán) tự động chặn truy cập đối với vai trò dưới `MANAGER`, hiển thị màn hình cảnh báo phân quyền.
  - Badge vai trò trực quan được tích hợp vào Workspace Switcher trên Navbar.

---

## 4. Loại Bỏ Trạng Thái Thành Công Giả (Fake Success Elimination)

Đã loại bỏ các fallback dữ liệu giả mạo khi API backend gặp lỗi:
1. `FormSubmissionModal.tsx`: Khi backend submit thất bại, hiển thị thông báo lỗi thực tế (`submitError`) thay vì tạo phiếu nộp giả mạo.
2. `Dashboard.tsx`: Loại bỏ logic fallback thành công trong `handleReview`. Khi review thất bại, hiển thị toast cảnh báo lỗi thực tế và không cập nhật trạng thái đơn cục bộ.
3. `CompanyMembersManager.tsx`: Loại bỏ `INITIAL_MOCK_MEMBERS` fallback khi người dùng đã đăng nhập; hiển thị chính xác danh sách thành viên thực từ database.
4. `AuditLogsManager.tsx`: Khi API trả về rỗng hoặc lỗi 403, không hiển thị demo logs đè lên dữ liệu thật.

---

## 5. Database Migration

- Migration: `20261013164117_AddRefreshTokensTable`
- Bảng mới: `refresh_tokens`
  - `id` (uuid, PK)
  - `user_id` (uuid, FK -> users)
  - `token_hash` (varchar(128), Index)
  - `token_family_id` (uuid, Index)
  - `created_at`, `expires_at`, `revoked_at` (timestamptz)
  - `replaced_by_token_id` (uuid)
  - `created_by_ip`, `user_agent` (varchar)
  - `is_revoked` (boolean)

---

## 6. Kết Quả Kiểm Thử & Build Thực Tế

### Backend Integration Tests (`dotnet test`)
- Đã bổ sung bộ test `AuthSessionApiTests.cs` (7 test scenarios):
  - `Login_ShouldSetHttpOnlyCookieAndIssueTokens`
  - `RefreshToken_ShouldRotateToken_AndInvalidatePreviousToken`
  - `RefreshToken_WhenReused_ShouldRevokeEntireFamily_AndReturnUnauthorized`
  - `RefreshToken_WhenExpired_ShouldReturnUnauthorized`
  - `Logout_ShouldRevokeSessionAndClearCookie`
  - `LogoutAll_ShouldRevokeAllUserSessions`
  - `RevokeSession_ShouldPreventRevokingOtherUsersSession`
- **Kết quả**: **74/74 passed (100%)**, 0 failed, 0 skipped.

### Frontend Production Build (`npm --prefix apps/web run build`)
- **Kết quả**:
  - `tsc -b`: 0 TypeScript errors.
  - `vite build`: `dist/assets/index-DWBBTVTD.js` (430.88 kB), hoàn thành trong 602ms.

---

## 7. Các Hạn Chế Còn Lại (Remaining Constraints)

- **CORS Allowed Origins**: Hiện cấu hình cho môi trường phát triển local (`localhost:5173`, `localhost:3000`). Khi triển khai domain production chính thức, cần bổ sung domain qua biến môi trường `Cors__AllowedOrigins`.
- **Reverse Proxy Cookie Forwarding**: Khi triển khai sau NGINX/Cloudflare, cần cấu hình `X-Forwarded-For` và `X-Forwarded-Proto` để middleware ASP.NET Core nhận diện đúng HTTPS và client IP.
