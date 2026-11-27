# 1.3. Sử dụng công nghệ định hướng giải quyết bài toán

Mục 1.2 đã giới thiệu các công nghệ được lựa chọn. Mục này trình bày cụ thể từng công nghệ được áp dụng như thế nào để giải quyết bốn bài toán đã nêu ở mục 1.1.3. Hệ thống gồm ba khối chính phối hợp với nhau: ứng dụng web viết bằng ReactJS và TypeScript (Front-end), lõi mô phỏng 3D viết bằng Unity và xuất bản dưới dạng WebGL, và dịch vụ AI phong thủy viết bằng NestJS. Front-end giao tiếp với Back-end qua RESTful API, còn Back-end là cầu nối duy nhất tới dịch vụ AI.

## 1.3.1. Bài toán quản lý người dùng và kho dự án thiết kế

Bài toán đặt ra hai yêu cầu: xác thực và phân quyền người dùng an toàn, và lưu trữ nhất quán nhiều phương án thiết kế của mỗi người dùng mà không mất dữ liệu khi chỉnh sửa liên tục.

* **ReactJS và TypeScript (Front-end)**
  * Xây dựng luồng đăng ký và đăng nhập nhiều bước: nhập email, đặt mật khẩu, xác thực mã OTP gửi qua email (có chức năng gửi lại mã), đăng nhập bằng mật khẩu. Mỗi bước là một thành phần (component) riêng nên dễ kiểm soát trạng thái và thông báo lỗi bằng tiếng Việt.
  * Tầng dịch vụ (`authService`, `client`) gom toàn bộ lời gọi API về một chỗ. Mã truy cập (access token) được tự động gắn vào header `Authorization: Bearer` của mọi yêu cầu, còn mã làm mới (refresh token) được gửi kèm qua cookie, nên các trang không phải tự xử lý xác thực.
  * TypeScript định nghĩa chặt chẽ kiểu dữ liệu người dùng và vai trò (`HOMEOWNER`, `ARCHITECT`, `CONTRACTOR`, `ADMIN`), dự án (`DesignProject`) và bản thiết kế (`BlueprintData`). Nhờ vậy, sai lệch cấu trúc dữ liệu giữa Front-end, Back-end và Unity được phát hiện ngay khi biên dịch.
  * Trang "Dự án của tôi" cho phép tạo, xem danh sách (sắp xếp theo lần sửa gần nhất), đổi tên và xóa dự án. Mỗi dự án lưu kèm bản thiết kế 3D gần nhất.

* **NestJS (Back-end)**
  * Áp dụng mô hình ba lớp Controller – Service – Repository cho các module xác thực, người dùng và dự án.
  * Guard của NestJS kiểm tra JWT và vai trò trước khi vào từng API, đảm bảo người dùng chỉ đọc, sửa được dự án của chính mình và chỉ quản trị viên mới truy cập được các chức năng quản trị.
  * Pipe kiểm tra dữ liệu đầu vào, giúp loại bỏ dữ liệu sai định dạng trước khi ghi vào cơ sở dữ liệu.

* **PostgreSQL**
  * Thông tin tài khoản và dự án được lưu ở các bảng quan hệ (`users`, `projects`), có khóa ngoại và ràng buộc `ON DELETE CASCADE` để dữ liệu luôn toàn vẹn.
  * Nội dung bản thiết kế 3D có cấu trúc lồng nhau (danh sách mô hình, tọa độ, góc xoay, thông số lô đất), nên được lưu nguyên khối trong một cột kiểu JSONB. Cách này cho phép đọc và ghi cả bản thiết kế trong một truy vấn mà không phải tách thành hàng chục bảng phụ, đồng thời vẫn truy vấn được các trường bên trong khi cần thống kê.
  * Tính ACID của giao dịch đảm bảo mỗi lần lưu hoặc là thành công trọn vẹn, hoặc là không thay đổi gì, tránh tình trạng bản vẽ bị ghi dở dang.

* **Unity (lưu và khôi phục bản thiết kế)**
  * Toàn bộ khung cảnh được tuần tự hóa thành một đối tượng JSON `BlueprintData`, gồm phiên bản định dạng, thông số lô đất và từng mô hình (mã mô hình, vị trí, góc xoay dạng quaternion, tỉ lệ, trạng thái khóa, lựa chọn giá). Trường phiên bản giúp các bản vẽ cũ vẫn mở được khi định dạng thay đổi.
  * Khi người dùng bấm Lưu, Unity gửi bản thiết kế sang trang web. Trang web ghi vào kho dự án rồi báo kết quả lại cho Unity, và Unity chỉ báo "đã lưu" khi nhận được xác nhận thành công.
  * Trong lúc bản thiết kế đang được nạp (mô hình còn đang tải về), chức năng lưu bị khóa tạm thời. Nhờ vậy, một bản vẽ chưa nạp xong không thể ghi đè lên bản vẽ đầy đủ đã lưu trước đó.
  * Nếu bản vẽ chứa mô hình không còn trong thư viện, hệ thống vẫn mở được phần còn lại và báo cho người dùng số mô hình bị bỏ qua, thay vì báo lỗi và không mở được cả bản vẽ.

## 1.3.2. Bài toán mô phỏng không gian 3D và thiết lập hiện trạng lô đất

Bài toán yêu cầu một môi trường 3D tương tác mượt mà chạy ngay trên trình duyệt, cho phép thiết lập lô đất theo hình dạng và hướng thực tế, và cho phép bố trí, kéo thả, xoay các mô hình trong phạm vi lô đất.

* **Unity Engine và Unity WebGL**
  * Mã C# được biên dịch sang WebAssembly, chạy trực tiếp trong trình duyệt mà người dùng không phải cài đặt phần mềm. Khung cảnh được dựng bằng Universal Render Pipeline (URP), có ánh sáng mặt trời, đổ bóng và vật liệu PBR, nên ngôi nhà và sân vườn hiển thị gần với thực tế.
  * **Thiết lập lô đất** (`LandBoundary`): người dùng chọn một trong năm dạng lô đất thường gặp ở khu ven đô (chữ nhật, hình thang nở hậu hoặc thóp hậu, chữ L, chữ T, tam giác), nhập kích thước và góc hướng mặt tiền. Đường ranh được vẽ trực tiếp trên mặt đất, kèm la bàn số. Hệ quy ước thống nhất trục +Z là hướng Bắc và +X là hướng Đông, nên mọi góc hướng trong hệ thống đều đọc được theo la bàn thật.
  * **Thao tác với mô hình** (`ModelMovement`): sử dụng tia chiếu (Raycast) từ camera kết hợp với vùng va chạm (Collider) của mô hình để xác định mô hình được chọn và điểm trên mặt đất dưới con trỏ. Từ đó người dùng có thể kéo thả, xoay, đổi kích thước và khóa mô hình để tránh kéo nhầm.
  * Khi thêm mô hình mới, hệ thống tự tìm một vị trí còn trống trong tầm nhìn của camera để đặt, tránh chồng lên các mô hình đã có. Mỗi mô hình còn được kiểm tra xem có nằm trọn trong ranh lô đất hay không.
  * Thư viện mô hình trong Unity (`UICatalog`) chia theo nhóm: nhà ở, cây xanh, bàn ghế sân vườn, trang trí, chiếu sáng, hàng rào và cổng, lối đi và mặt nước. Mỗi mô hình có ảnh xem trước và giá tham khảo.

* **Cầu nối giữa Unity và ReactJS**
  * Trang web (`useUnityDesigner`) tải bản build Unity từ tệp `manifest.json`, hiển thị tiến trình tải và khởi tạo Unity trên một thẻ `<canvas>`.
  * Chiều web → Unity: gọi `SendMessage` tới đối tượng `WebBridge` với các lệnh như nạp thư viện mô hình, nạp bản thiết kế, lưu, chọn mô hình, khóa mô hình, chọn cửa hàng.
  * Chiều Unity → web: tệp cầu nối `.jslib` phát sự kiện `suburbsim:unity` với các loại `ready`, `loaded`, `save`, `state`, `items`.
  * Nhờ cầu nối này, phần đồ họa 3D (Unity) và phần giao diện nghiệp vụ (React: thanh công cụ, bảng dự toán, bảng phong thủy) được phát triển độc lập nhưng luôn đồng bộ dữ liệu với nhau.

* **Blender và định dạng glTF/GLB**
  * Mô hình gốc từ BlenderKit được xử lý theo lô bằng Blender chạy ở chế độ không giao diện:
    * sửa lỗi vật liệu (kênh trong suốt bị nối sai, kính và nước chưa trong suốt)
    * giới hạn độ phân giải texture ở 1024 px
    * nén lưới đa giác bằng Draco
    * xuất ra định dạng GLB
  * Mỗi mô hình được kiểm tra kích thước sau khi xuất phải khớp với kích thước thật (sai số dưới 1 mm), để tỉ lệ nhà, cây, cổng trong khung cảnh luôn đúng.

* **Lưu trữ đối tượng trên đám mây (Cloudflare R2) và glTFast**
  * 39 mô hình (khoảng 27 MB) cùng ảnh xem trước và tệp danh mục `catalog.json` được đặt trên kho lưu trữ đối tượng, không đóng gói vào bản build Unity. Nhờ vậy, bản build nhẹ hơn, và việc thêm hoặc sửa mô hình chỉ cần cập nhật kho lưu trữ, không phải build lại ứng dụng.
  * Khi người dùng đặt một mô hình, Unity dùng thư viện glTFast để tải tệp GLB trong lúc chạy. Mỗi mô hình chỉ tải một lần, những lần đặt sau sẽ nhân bản từ bản đã tải. Trong lúc chờ tải, giao diện hiển thị thông báo "đang tải mô hình".

## 1.3.3. Bài toán dự toán chi phí và kiểm tra quy chuẩn pháp lý theo thời gian thực

Bài toán yêu cầu cập nhật chi phí dự kiến ngay khi người dùng thêm, bớt hoặc đổi kích thước mô hình, đồng thời kiểm tra sơ bộ các chỉ tiêu quy hoạch.

* **Unity: tính chi phí theo sự kiện** (`ModelEstPrice`)
  * Mỗi khi có mô hình được thêm, xóa hay nạp vào, Unity phát sự kiện `ModelsChanged`. Bộ tính giá đánh dấu cần tính lại và tính tổng đúng một lần ở cuối khung hình, nên dù người dùng thao tác liên tục, chi phí vẫn cập nhật tức thì mà không làm giảm tốc độ khung hình.
  * Mỗi hạng mục mặc định dùng giá rẻ nhất trong các báo giá của cửa hàng (hoặc giá niêm yết nếu chưa có báo giá). Người dùng có thể chọn cửa hàng khác hoặc nhập giá riêng, và các lựa chọn này được lưu cùng bản thiết kế.
  * **Bóc tách theo vật liệu xây dựng:** mỗi mẫu nhà được gắn danh sách vật liệu cấu thành (kính, gạch xây, sàn gỗ, ngói lợp, bê tông...), mỗi vật liệu có khối lượng và đơn giá riêng. Chi phí của căn nhà được tính bằng công thức:

    Chi phí = Σ (Khối lượng × Hệ số tỉ lệ × Đơn giá)

    Trong đó, hệ số tỉ lệ phụ thuộc đơn vị tính và tỉ lệ s mà người dùng phóng to hoặc thu nhỏ mô hình: s² với đơn vị m², s³ với đơn vị m³, s với đơn vị m, và giữ nguyên với đơn vị đếm (bộ, cái, bậc). Vì vậy, khi người dùng thay đổi kích thước căn nhà, chi phí vật liệu cũng thay đổi theo đúng bản chất của từng loại vật liệu.
  * Khối lượng ban đầu của từng vật liệu được đo tự động từ mô hình 3D bằng Blender (diện tích bề mặt, diện tích mặt sàn, số tấm kính), sau đó được hiệu chỉnh lại.

* **ReactJS: bảng dự toán** (`EstimatePanel`)
  * Nhận danh sách hạng mục từ Unity qua sự kiện `items` và hiển thị tổng chi phí cùng từng hạng mục.
  * Với nhà ở, bảng dự toán hiển thị chi tiết từng vật liệu dưới dạng khối lượng × đơn giá = thành tiền. Người dùng có thể đổi cửa hàng hoặc nhập đơn giá riêng cho từng vật liệu.
  * Chọn một hạng mục trên bảng dự toán sẽ đồng thời chọn mô hình tương ứng trong khung cảnh 3D, và ngược lại.

* **PostgreSQL**
  * Bảng `materials` lưu danh mục vật liệu cùng khung đơn giá (giá thấp nhất và cao nhất), còn bảng `project_materials` lưu khối lượng vật liệu của từng dự án. Quản trị viên chỉ cần cập nhật đơn giá ở một nơi là mọi bản dự toán dùng vật liệu đó được cập nhật theo.

* **Kiểm tra quy chuẩn pháp lý**
  * Bản thiết kế đã có đủ dữ liệu hình học cho các phép kiểm tra này:
    * đa giác ranh lô đất, từ đó tính được diện tích lô đất
    * kích thước khối nhà (kích thước thật nhân với tỉ lệ), từ đó tính được diện tích chiếm đất
    * vị trí và hướng của nhà so với cạnh mặt tiền
  * Từ các dữ liệu trên, hệ thống tính mật độ xây dựng (diện tích chiếm đất / diện tích lô đất) và khoảng lùi (khoảng cách từ khối nhà tới cạnh mặt tiền), rồi đối chiếu với giới hạn trong QCVN 01:2021/BXD để cảnh báo người dùng ngay khi bố trí.

## 1.3.4. Bài toán đánh giá và gợi ý bố trí phong thủy bằng AI

Bài toán yêu cầu tự động trích xuất dữ liệu hình học từ bản vẽ 3D, kết hợp với năm sinh và giới tính của gia chủ để đánh giá phong thủy Bát trạch, cảnh báo các điểm phạm và gợi ý cách điều chỉnh, sao cho kết quả đáng tin cậy và giải thích được.

* **Unity: trích xuất dữ liệu phong thủy từ khung cảnh**
  * Mỗi mô hình được gán loại phong thủy (nhà, cổng, bếp, cây, hồ nước...) và hướng mặt trước của mô hình (`frontYawOffset`). Riêng nhà ở được gán thêm vị trí cửa chính (`doorOffset`) và kích thước thật (`baseSize`).
  * Khi lưu, Unity tính hướng la bàn của từng mô hình từ góc xoay theo công thức:

    yaw = atan2(2(w·y + x·z), 1 − 2(x² + y²)) + frontYawOffset

    Kết quả được ghi vào bản thiết kế cùng loại mô hình và cờ cho biết mô hình có nằm trong ranh lô đất hay không. Dữ liệu phong thủy vì vậy được trích ra trực tiếp từ bố cục mà người dùng đã dựng, người dùng không phải tự nhập lại.

* **TypeScript: chuyển đổi bản thiết kế sang bố cục phong thủy** (`blueprintToBoCuc`)
  * Đổi hệ tọa độ của Unity (x hướng Đông, z hướng Bắc) sang hệ tọa độ bản vẽ của bộ quy tắc (x hướng Đông, y hướng Nam).
  * Xác định các thành phần của bố cục:
    * tâm nhà và hướng nhà, lấy theo căn nhà lớn nhất
    * vị trí cửa chính và khối nhà, tính từ danh mục mô hình
    * cổng, lấy cổng gần nhà nhất và quy về hướng nhìn ra ngoài
    * vị trí bếp
    * đa giác thửa đất, tính lại đúng như cách Unity dựng ranh đất
  * Những phần chưa xác định được (ví dụ chưa đặt cổng) được liệt kê riêng để giao diện nhắc người dùng bổ sung.

* **Bộ quy tắc phong thủy (rule engine `phongthuy-battrach`, TypeScript)**
  * Tính mệnh cung của gia chủ từ năm âm lịch (có bộ chuyển đổi dương lịch sang âm lịch, xác định đúng ngày Tết) và giới tính, rồi xếp vào nhóm Đông tứ mệnh hoặc Tây tứ mệnh.
  * Hàm `kiemTraBoCuc` kiểm tra các yếu tố, mỗi cảnh báo kèm mức độ và số trang sách làm căn cứ:
    * hướng nhà (hướng khí)
    * vị trí cửa chính
    * phương vị của cổng
    * hướng bếp
    * hình dạng thửa đất (đất tam giác, đất trước rộng sau hẹp, đất khuyết góc chữ L hoặc chữ T)
    * hình dạng khối nhà (dạng túi tiền, thót hậu, lệch trái phải)

    Từ các cảnh báo này, bộ quy tắc tính ra điểm tổng.
  * Mọi đề xuất điều chỉnh (ví dụ dời cổng sang phương khác) đều được bộ quy tắc áp thử lên bố cục và chấm lại trước khi đưa cho người dùng (`kiemChungDeXuat`), nên không có đề xuất nào làm bố cục xấu đi.
  * Kết quả hoàn toàn tất định và được kiểm chứng bằng 42 unit test.

* **Mô hình ngôn ngữ Google Gemini kết hợp RAG**
  * Mô hình ngôn ngữ không tự tính phương vị mà chỉ diễn giải kết quả của bộ quy tắc thành câu trả lời tiếng Việt dễ hiểu, và trả lời câu hỏi của người dùng trong khung chat.
  * Tri thức được truy hồi từ sách *Phong thủy thực hành trong xây dựng và kiến trúc nhà ở* (Vũ Đức Huynh):
    * sách được chia thành 454 đoạn, nhúng thành vector bằng `gemini-embedding-2` và lưu trong PostgreSQL với extension pgvector
    * truy hồi kết hợp tìm kiếm vector và tìm kiếm toàn văn, trộn kết quả bằng thuật toán Reciprocal Rank Fusion, giúp nâng MRR từ 0,708 lên 0,833 trên bộ câu hỏi kiểm thử
    * câu trả lời luôn dẫn số trang sách có thật
  * Câu trả lời được truyền về theo luồng bằng Server-Sent Events, nên người dùng thấy chữ hiện dần thay vì phải chờ cả câu trả lời.

* **NestJS (dịch vụ AI tách riêng)**
  * Dịch vụ AI chạy riêng với cơ sở dữ liệu riêng. Chỉ Back-end được gọi tới, bằng khóa `x-api-key`; Front-end không gọi trực tiếp.
  * Mỗi người dùng bị giới hạn số lần hỏi theo phút và theo ngày, và câu trả lời trùng lặp được lưu vào bộ nhớ đệm để kiểm soát chi phí gọi mô hình.

* **ReactJS: bảng phong thủy** (`PhongThuyPanel`)
  * Hiển thị mệnh cung, điểm, danh sách cảnh báo theo mức độ, kèm khung chat hỏi đáp có hiển thị câu trả lời theo luồng.
  * Bố cục được tính lại mỗi khi bản thiết kế thay đổi, nên kết quả phong thủy luôn khớp với khung cảnh 3D hiện tại.
