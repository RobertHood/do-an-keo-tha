# 2.6. HƯỚNG DẪN VẼ BIỂU ĐỒ CƠ SỞ DỮ LIỆU (DATABASE DIAGRAM / ERD) – SUBURBSIM

Tài liệu này hướng dẫn từng bước chuyển đổi từ **Biểu đồ lớp thực thể pha thiết kế** ([class_diagram_chuong_2.md](file:///e:/đồ%20án/Suburban-House-Design-Simulation-BE/class_diagram_chuong_2.md)) sang **Biểu đồ Cơ sở dữ liệu (ERD)** trên Visual Paradigm (`đồ án.vpp`) theo đúng 5 bước của **TS. Nguyễn Mạnh Hùng (PTIT)** trong bài *Database design* ([softwaredesign.home.blog](https://softwaredesign.home.blog/tutorials/hotel-reservation-management-application/database-design/)), tuân thủ **100%** chuẩn PostgreSQL + Prisma trong `AGENTS.md` (Quy tắc 6) và `prisma/schema/user.prisma`.

---

## PHẦN 1: QUY TRÌNH 5 BƯỚC CHUYỂN ĐỔI TỪ BIỂU ĐỒ LỚP SANG CSDL

### Bước 1: Tạo bảng từ các lớp thực thể nghiệp vụ
* Theo `AGENTS.md` (Quy tắc 6) và file [user.prisma](file:///e:/đồ%20án/Suburban-House-Design-Simulation-BE/prisma/schema/user.prisma) đã có trong repo, tên bảng dùng **`snake_case` số nhiều** (`users`, `projects`, `materials`...) để khớp 100% với `@@map("...")` trong Prisma.
  *(Ghi chú: Nếu giảng viên hướng dẫn yêu cầu giữ nguyên tiền tố `tbl` theo bài giảng thầy Hùng, bảng đối chiếu bên dưới có sẵn tên tương đương `tblUser`, `tblProject`... để bạn linh hoạt đổi tên.)*

### Bước 2: Loại bỏ toàn bộ thuộc tính đối tượng (Object Attributes)
* Chuyển toàn bộ các thuộc tính đơn (scalar / JSON) thành cột của bảng; **xóa bỏ** các thuộc tính tham chiếu đối tượng từ Bước 4 của Class Diagram:
  * Xóa `User.fengShuiProfile`
  * Xóa `Project.user`, `Project.template`, `Project.plot`, `Project.houseStructure`, `Project.listMaterials`, `Project.listAudits`, `Project.listChats`
  * Xóa `ProjectMaterial.material`

### Bước 3: Xử lý quan hệ số lượng (1–1, 1–N, N–N)
* **Quan hệ 1–0..1 (tách bảng có điều kiện):**
  * `users` — `feng_shui_profiles`: Giữ tách riêng 2 bảng (vì không phải user nào, ví dụ Admin, cũng có hồ sơ phong thủy; khớp `AGENTS.md` và `BACKEND_PLAN.md`), đặt `user_id` làm **FK + UNIQUE** trong `feng_shui_profiles`.
  * `projects` — `plots` và `projects` — `house_structures`: Giữ tách riêng thành 3 bảng để gọn nhẹ truy vấn và dễ mở rộng cấu trúc GIS/nhiều tầng về sau, đặt `project_id` làm **FK + UNIQUE** trong `plots` và `house_structures`.
* **Quan hệ 1–N:** Tách thành 2 bảng độc lập (`users` – `projects`, `design_templates` – `projects`, `projects` – `feng_shui_audits`, `projects` – `feng_shui_chat_histories`).
* **Quan hệ N–N:** Đã được phá vỡ qua bảng trung gian **`project_materials`** (`projects` 1–N `project_materials` N–1 `materials`).

### Bước 4: Cấu hình khóa chính (PK), khóa ngoại (FK), chỉ mục & kiểu dữ liệu tài chính
* **Khóa chính (`PK`):** 100% các bảng dùng **`id UUID`** (`DEFAULT gen_random_uuid()`) để chống lộ ID tự tăng (Quy tắc 6 `AGENTS.md` & `user.prisma`).
* **Khóa ngoại (`FK`):** Đặt tại bảng phía `N` (hoặc phía `0..1`) dưới dạng `<ten_bang_cha_so_it>_id` (`user_id`, `template_id`, `project_id`, `material_id`), kiểu `UUID`, kèm **chỉ mục (`INDEX`)** và ràng buộc **`ON DELETE`** (`CASCADE` / `RESTRICT` / `SET NULL`).
* **Kiểu tiền tệ:** Các cột giá (`target_budget`, `min_unit_price`, `max_unit_price`) dùng **`DECIMAL(14,2)`** hoặc **`DECIMAL(12,2)`**, tuyệt đối không dùng `Float`.

### Bước 5: Loại bỏ thuộc tính dư thừa & toàn bộ các bảng thống kê (Derived Data)
* **Loại bỏ 3 lớp thống kê (KHÔNG tạo thành bảng trong CSDL):**
  * **`TemplateStat`** ➔ Không tạo bảng (số lượt áp dụng tính bằng `COUNT` trên bảng `projects` theo `template_id`).
  * **`UserStat`** ➔ Không tạo bảng (số dự án của mỗi chủ nhà tính bằng `COUNT` trên `projects` theo `user_id`).
  * **`SystemOverviewStat`** ➔ Không tạo bảng (tổng người dùng, tổng dự án, tổng vật liệu truy vấn tổng hợp trực tiếp từ `users`, `projects`, `materials`).
* **Không lưu cột dẫn xuất có thể tính trực tiếp:**
  * Không lưu `building_density` (mật độ xây dựng = `footprint_area / plots.area`), không lưu `estimated_subtotal` hay `total_estimated_cost` (tính thời gian thực từ `project_materials.quantity_area` nhân đơn giá `materials`).

---

## PHẦN 2: ĐẶC TẢ CHI TIẾT 10 BẢNG TRONG CƠ SỞ DỮ LIỆU

*(Kết quả sau 5 bước chuẩn hóa còn đúng **10 bảng** dưới đây.)*

### 1. Bảng `users` *(Tên `tblUser` nếu dùng tiền tố `tbl` — Đã có trong `prisma/schema/user.prisma`)*
| Tên cột (`snake_case`) | Kiểu dữ liệu (PostgreSQL) | Ràng buộc | Ghi chú |
|:---|:---|:---|:---|
| `id` | `UUID` | **PK, U, NOT NULL** | `DEFAULT gen_random_uuid()` |
| `email` | `VARCHAR(255)` | **U, NOT NULL, IDX** | Email đăng nhập |
| `password` | `VARCHAR(255)` | **NOT NULL** | Mật khẩu đã hash |
| `full_name` | `VARCHAR(150)` | **NOT NULL** | Họ tên |
| `phone_number` | `VARCHAR(20)` | NULL | Số điện thoại |
| `avatar_url` | `VARCHAR(500)` | NULL | URL ảnh đại diện |
| `role` | `Role (ENUM)` | **NOT NULL** | `HOMEOWNER`, `ARCHITECT`, `CONTRACTOR`, `ADMIN` |
| `is_active` | `BOOLEAN` | **NOT NULL** | Mặc định `false` |
| `otp_code` | `VARCHAR(6)` | NULL | Mã OTP |
| `otp_expires_at` | `TIMESTAMP` | NULL | Hạn OTP |
| `hashed_refresh_token` | `VARCHAR(255)` | NULL | Refresh token băm |
| `created_at` | `TIMESTAMP` | **NOT NULL** | Mặc định `now()` |
| `updated_at` | `TIMESTAMP` | **NOT NULL** | Tự động cập nhật |

### 2. Bảng `feng_shui_profiles` *(hoặc `tblFengShuiProfile`)*
| Tên cột | Kiểu dữ liệu | Ràng buộc | Ghi chú |
|:---|:---|:---|:---|
| `id` | `UUID` | **PK, U, NOT NULL** | Khóa chính |
| `user_id` | `UUID` | **FK, U, NOT NULL, IDX** | Tham chiếu `users(id)` (`ON DELETE CASCADE`) |
| `birth_year` | `INTEGER` | **NOT NULL** | Năm sinh gia chủ |
| `gender` | `VARCHAR(10)` | **NOT NULL** | `MALE` / `FEMALE` |
| `kua_number` | `INTEGER` | **NOT NULL** | Quái số Bát Trạch |
| `destiny_palace` | `VARCHAR(50)` | **NOT NULL** | Cung phi |
| `fate_group` | `VARCHAR(20)` | **NOT NULL** | `EAST_FOUR` / `WEST_FOUR` |
| `auspicious_directions` | `JSONB` | **NOT NULL** | 4 hướng tốt |
| `inauspicious_directions` | `JSONB` | **NOT NULL** | 4 hướng xấu |
| `created_at` | `TIMESTAMP` | **NOT NULL** | Mặc định `now()` |
| `updated_at` | `TIMESTAMP` | **NOT NULL** | Tự động cập nhật |

### 3. Bảng `design_templates` *(hoặc `tblDesignTemplate`)*
| Tên cột | Kiểu dữ liệu | Ràng buộc | Ghi chú |
|:---|:---|:---|:---|
| `id` | `UUID` | **PK, U, NOT NULL** | Khóa chính |
| `name` | `VARCHAR(200)` | **NOT NULL** | Tên bản thiết kế mẫu |
| `style` | `VARCHAR(100)` | **NOT NULL** | Phong cách kiến trúc |
| `suggested_plot_area` | `DOUBLE PRECISION` | **NOT NULL** | Diện tích đất đề xuất (m²) |
| `preview_image_url` | `VARCHAR(500)` | **NOT NULL** | Ảnh phối cảnh đại diện |
| `scene_config_json` | `JSONB` | **NOT NULL** | Dữ liệu cấu hình bản vẽ 3D mẫu |
| `is_published` | `BOOLEAN` | **NOT NULL** | Mặc định `true` |
| `created_at` | `TIMESTAMP` | **NOT NULL** | Mặc định `now()` |
| `updated_at` | `TIMESTAMP` | **NOT NULL** | Tự động cập nhật |

### 4. Bảng `projects` *(hoặc `tblProject`)*
| Tên cột | Kiểu dữ liệu | Ràng buộc | Ghi chú |
|:---|:---|:---|:---|
| `id` | `UUID` | **PK, U, NOT NULL** | Khóa chính |
| `user_id` | `UUID` | **FK, NOT NULL, IDX** | Tham chiếu `users(id)` (`ON DELETE CASCADE`) |
| `template_id` | `UUID` | **FK, NULL, IDX** | Tham chiếu `design_templates(id)` (`ON DELETE SET NULL`) |
| `name` | `VARCHAR(200)` | **NOT NULL** | Tên dự án |
| `location` | `VARCHAR(255)` | **NOT NULL** | Địa điểm lô đất |
| `description` | `TEXT` | NULL | Mô tả dự án |
| `thumbnail_url` | `VARCHAR(500)` | NULL | Ảnh đại diện |
| `target_budget` | `DECIMAL(14,2)` | NULL | Ngân sách mục tiêu (VNĐ) |
| `init_type` | `VARCHAR(30)` | **NOT NULL** | `EMPTY_PLOT` / `FROM_TEMPLATE` |
| `scene_json` | `JSONB` | NULL | JSON vị trí các mô hình 3D trong cảnh |
| `created_at` | `TIMESTAMP` | **NOT NULL** | Mặc định `now()` |
| `updated_at` | `TIMESTAMP` | **NOT NULL** | Tự động cập nhật |
| `deleted_at` | `TIMESTAMP` | NULL | Thời gian xóa mềm |

### 5. Bảng `plots` *(hoặc `tblPlot`)*
| Tên cột | Kiểu dữ liệu | Ràng buộc | Ghi chú |
|:---|:---|:---|:---|
| `id` | `UUID` | **PK, U, NOT NULL** | Khóa chính |
| `project_id` | `UUID` | **FK, U, NOT NULL, IDX** | Tham chiếu `projects(id)` (`ON DELETE CASCADE`) |
| `width` | `DOUBLE PRECISION` | **NOT NULL** | Chiều rộng mặt tiền (m) |
| `depth` | `DOUBLE PRECISION` | **NOT NULL** | Chiều sâu lô đất (m) |
| `area` | `DOUBLE PRECISION` | **NOT NULL** | Diện tích khuôn viên (m²) |
| `orientation_degree` | `DOUBLE PRECISION` | **NOT NULL** | Góc la bàn (0°–360°) |
| `orientation_label` | `VARCHAR(30)` | **NOT NULL** | Tên hướng đất (Đông Nam...) |
| `boundary_polygon` | `JSONB` | NULL | Tọa độ đa giác ranh đất GIS dự phòng |
| `created_at` | `TIMESTAMP` | **NOT NULL** | Mặc định `now()` |
| `updated_at` | `TIMESTAMP` | **NOT NULL** | Tự động cập nhật |

### 6. Bảng `house_structures` *(hoặc `tblHouseStructure`)*
| Tên cột | Kiểu dữ liệu | Ràng buộc | Ghi chú |
|:---|:---|:---|:---|
| `id` | `UUID` | **PK, U, NOT NULL** | Khóa chính |
| `project_id` | `UUID` | **FK, U, NOT NULL, IDX** | Tham chiếu `projects(id)` (`ON DELETE CASCADE`) |
| `house_model_name` | `VARCHAR(200)` | **NOT NULL** | Tên mẫu công trình chính |
| `footprint_width` | `DOUBLE PRECISION` | **NOT NULL** | Chiều rộng khối nhà (m) |
| `footprint_depth` | `DOUBLE PRECISION` | **NOT NULL** | Chiều sâu khối nhà (m) |
| `footprint_area` | `DOUBLE PRECISION` | **NOT NULL** | Diện tích chiếm đất (m²) |
| `floor_count` | `INTEGER` | **NOT NULL** | Số tầng công trình |
| `total_floor_area` | `DOUBLE PRECISION` | **NOT NULL** | Tổng diện tích sàn (m²) |
| `front_setback` | `DOUBLE PRECISION` | **NOT NULL** | Khoảng lùi mặt tiền (m) |
| `side_setback` | `DOUBLE PRECISION` | **NOT NULL** | Khoảng lùi hông (m) |
| `rear_setback` | `DOUBLE PRECISION` | **NOT NULL** | Khoảng lùi sau (m) |
| `facing_direction` | `VARCHAR(30)` | **NOT NULL** | Hướng mặt tiền công trình |
| `created_at` | `TIMESTAMP` | **NOT NULL** | Mặc định `now()` |
| `updated_at` | `TIMESTAMP` | **NOT NULL** | Tự động cập nhật |

### 7. Bảng `materials` *(hoặc `tblMaterial`)*
| Tên cột | Kiểu dữ liệu | Ràng buộc | Ghi chú |
|:---|:---|:---|:---|
| `id` | `UUID` | **PK, U, NOT NULL** | Khóa chính |
| `name` | `VARCHAR(200)` | **NOT NULL** | Tên vật liệu |
| `category` | `VARCHAR(100)` | **NOT NULL, IDX** | Loại vật liệu (Gỗ lát sàn, Ngói lợp...) |
| `applied_zone` | `VARCHAR(150)` | **NOT NULL** | Hạng mục áp dụng |
| `unit` | `VARCHAR(20)` | **NOT NULL** | Đơn vị tính (`m²`) |
| `min_unit_price` | `DECIMAL(12,2)` | **NOT NULL** | Đơn giá thấp nhất |
| `max_unit_price` | `DECIMAL(12,2)` | **NOT NULL** | Đơn giá cao nhất |
| `texture_url` | `VARCHAR(500)` | **NOT NULL** | Ảnh bề mặt vật liệu |
| `created_at` | `TIMESTAMP` | **NOT NULL** | Mặc định `now()` |
| `updated_at` | `TIMESTAMP` | **NOT NULL** | Tự động cập nhật |

### 8. Bảng `project_materials` *(hoặc `tblProjectMaterial` — Bảng trung gian tách N–N)*
| Tên cột | Kiểu dữ liệu | Ràng buộc | Ghi chú |
|:---|:---|:---|:---|
| `id` | `UUID` | **PK, U, NOT NULL** | Khóa chính |
| `project_id` | `UUID` | **FK, NOT NULL, IDX** | Tham chiếu `projects(id)` (`ON DELETE CASCADE`) |
| `material_id` | `UUID` | **FK, NOT NULL, IDX** | Tham chiếu `materials(id)` (`ON DELETE RESTRICT`) |
| `applied_surface` | `VARCHAR(100)` | **NOT NULL** | Vị trí bề mặt áp dụng |
| `quantity_area` | `DOUBLE PRECISION` | **NOT NULL** | Khối lượng diện tích (m²) |
| `created_at` | `TIMESTAMP` | **NOT NULL** | Mặc định `now()` |
| `updated_at` | `TIMESTAMP` | **NOT NULL** | Tự động cập nhật |

### 9. Bảng `feng_shui_audits` *(hoặc `tblFengShuiAudit`)*
| Tên cột | Kiểu dữ liệu | Ràng buộc | Ghi chú |
|:---|:---|:---|:---|
| `id` | `UUID` | **PK, U, NOT NULL** | Khóa chính |
| `project_id` | `UUID` | **FK, NOT NULL, IDX** | Tham chiếu `projects(id)` (`ON DELETE CASCADE`) |
| `overview_table_json` | `JSONB` | **NOT NULL** | Kết quả bảng đánh giá tổng quan |
| `layout_suggestions_json` | `JSONB` | **NOT NULL** | Danh sách gợi ý bố trí từ AI |
| `created_at` | `TIMESTAMP` | **NOT NULL** | Mặc định `now()` |

### 10. Bảng `feng_shui_chat_histories` *(hoặc `tblFengShuiChatHistory`)*
| Tên cột | Kiểu dữ liệu | Ràng buộc | Ghi chú |
|:---|:---|:---|:---|
| `id` | `UUID` | **PK, U, NOT NULL** | Khóa chính |
| `project_id` | `UUID` | **FK, NOT NULL, IDX** | Tham chiếu `projects(id)` (`ON DELETE CASCADE`) |
| `question` | `TEXT` | **NOT NULL** | Câu hỏi của chủ nhà |
| `answer` | `TEXT` | **NOT NULL** | Phản hồi của trợ lý AI |
| `created_at` | `TIMESTAMP` | **NOT NULL** | Mặc định `now()` |

---

## PHẦN 3: BẢNG HƯỚNG DẪN NỐI 9 ĐƯỜNG QUAN HỆ ERD TRÊN VISUAL PARADIGM

Trên Visual Paradigm, bạn tạo **Entity Relationship Diagram (ERD)** mới, tạo đủ **10 bảng** trên và kéo **9 đường nối nét đứt (Non-Identifying Relationship / One-to-Many)** theo bảng dưới đây:

| STT | Bảng cha *(Đầu `\|` = 1)* | Ký hiệu đầu nối (Crow's Foot) | Bảng con *(Chứa FK)* | Cột FK & Ràng buộc | Chính sách `ON DELETE` |
|:---:|:---|:---|:---|:---|:---|
| 1 | `users` | `\|` `- - - -` `O\|` *(1 tới 0..1)* | `feng_shui_profiles` | `user_id` (`FK, U, NOT NULL`) | `CASCADE` |
| 2 | `users` | `\|` `- - - -` `O<` *(1 tới 0..\*)* | `projects` | `user_id` (`FK, NOT NULL, IDX`) | `CASCADE` |
| 3 | `design_templates` | `\|` `- - - -` `O<` *(1 tới 0..\*)* | `projects` | `template_id` (`FK, NULL, IDX`) | `SET NULL` |
| 4 | `projects` | `\|` `- - - -` `O\|` *(1 tới 0..1)* | `plots` | `project_id` (`FK, U, NOT NULL`) | `CASCADE` |
| 5 | `projects` | `\|` `- - - -` `O\|` *(1 tới 0..1)* | `house_structures` | `project_id` (`FK, U, NOT NULL`) | `CASCADE` |
| 6 | `projects` | `\|` `- - - -` `O<` *(1 tới 0..\*)* | `project_materials` | `project_id` (`FK, NOT NULL, IDX`) | `CASCADE` |
| 7 | `materials` | `\|` `- - - -` `O<` *(1 tới 0..\*)* | `project_materials` | `material_id` (`FK, NOT NULL, IDX`) | `RESTRICT` |
| 8 | `projects` | `\|` `- - - -` `O<` *(1 tới 0..\*)* | `feng_shui_audits` | `project_id` (`FK, NOT NULL, IDX`) | `CASCADE` |
| 9 | `projects` | `\|` `- - - -` `O<` *(1 tới 0..\*)* | `feng_shui_chat_histories` | `project_id` (`FK, NOT NULL, IDX`) | `CASCADE` |

---

## PHẦN 4: MẸO VẼ ERD ĐÚNG CHUẨN BÀI MẪU THẦY HÙNG TRÊN VISUAL PARADIGM
1. **Dùng công cụ `Entity Relationship Diagram` (không dùng Class Diagram) trên Visual Paradigm:** Khi tạo cột `id` chọn **Primary Key**, các cột `NULL` sẽ tự hiện chữ `N` bên phải, cột `FK` khi kéo dây nối từ bảng cha sang bảng con sẽ tự sinh biểu tượng mũi tên xanh và in nghiêng giống hệt ảnh mẫu của thầy Hùng.
2. **Dùng đường nối nét đứt (`Non-Identifying Relationship`):** Toàn bộ 9 đường nối đều là nét đứt vì mọi bảng con đều có khóa chính độc lập `id` (UUID), không ghép khóa chính bảng cha vào làm khóa chính phức hợp.
3. **Bố cục không chồng chéo dây:** Đặt **`projects`** ở chính giữa màn hình; đặt `users`, `feng_shui_profiles`, `design_templates` ở phía trên; đặt `plots`, `house_structures` bên trái/phải; đặt `feng_shui_audits`, `feng_shui_chat_histories`, `project_materials`, `materials` ở phía dưới.
