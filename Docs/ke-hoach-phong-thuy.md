# Kế hoạch bổ sung dữ liệu phong thủy cho blueprint

Cả bốn chỗ thiếu đều sửa được ở phía Unity. Cái bẫy toạ độ cũng loại bỏ được hẳn, bằng cách cho Unity tự đổi toạ độ trước khi xuất dữ liệu.

## Hiện trạng trong code

- **`Assets/Scripts/BlueprintData.cs`:** mỗi mô hình chỉ lưu `prefabId`, `position`, `rotation` (dạng quaternion), `scale`, `isLocked` và các trường giá.
- **Catalog** chỉ biết nhóm hiển thị (`ModelCategory`), chưa biết loại công trình theo phong thủy.
- **Đã có kích thước thật nhưng chưa được lưu.** Lúc chuyển đổi mô hình, kích thước thật ở scale 1 đã được ghi vào file `export.json`. Ví dụ: Cottage 15,7 × 7,9 m, Cổng sắt lớn rộng 2,95 m, Bể bơi 3,4 × 6,8 m. Nhưng các số này chưa được đưa vào catalog hay blueprint.
- **Chưa có gì mô tả ranh thửa đất, hướng Bắc hay mặt tiền nhà.**

## Kế hoạch (phía Unity)

### Bước 1: Thêm trường loại công trình (`loai`) vào catalog

- Thêm trường `loai` vào `PriceCatalogEntry`, với các giá trị như `nha`, `cong`, `hang_rao`, `cay`, `ho_nuoc`, `dai_phun_nuoc`, `be_boi`, `loi_di`, `den`, `tuong`, `ghe_ban`, `chau_cay`.
- Điền sẵn cho cả 35 mô hình bằng công cụ import, để giá trị này đi cùng dữ liệu.
- Mỗi mục catalog cũng nhận thêm kích thước cơ sở thật từ `export.json` (`baseSize`, tính bằng mét).

### Bước 2: Mặt tiền và cửa chính cho từng nhà

- Cửa của 5 mẫu nhà không quay cùng một hướng trong mô hình gốc. Mỗi mẫu nhà cần thêm hai giá trị:
  - `frontYawOffset`: phía nào là mặt tiền (0, 90, 180 hoặc 270°).
  - `doorOffset`: vị trí cửa chính so với tâm nhà.
- Chỉ cần đặt một lần cho mỗi nhà, bằng một vật đánh dấu nhỏ hoặc nhập số trong catalog. Cần nhìn xác nhận từng nhà, vì không xác định được mặt tiền chỉ từ hình khối.
- Khi đó hướng nhà (`huongNha`) tính thẳng được: `yaw của nhà + frontYawOffset`. Nếu muốn lấy hướng theo cổng thì cổng vốn đã có mặt trước rõ ràng.

### Bước 3: Ranh thửa đất

- Thêm một đối tượng `Lot`: một đa giác trên mặt đất, ban đầu là hình chữ nhật theo chiều rộng và chiều sâu. Các góc kéo được, nên vẽ được thửa tam giác, chữ L, chữ T, trước rộng sau hẹp.
- Trục +Z của Unity cố định là hướng Bắc, và người dùng xoay thửa đất thay vì xoay la bàn.
- Vẫn ghi `gocBacBanVe = 0` vào JSON, để sau này có thêm la bàn thì cấu trúc dữ liệu của engine không phải đổi.

### Bước 4: Xuất sẵn một khối dữ liệu cho engine trong blueprint

Khi lưu, Unity ghi thêm một phần riêng đã đổi sẵn sang toạ độ 2D của engine. Như vậy sau này không ai phải tự viết phép đổi bằng tay.

```json
"phongThuy": {
  "heToaDo": "x=Dong, y=Nam (y huong xuong), met",
  "gocBacBanVe": 0,
  "ranhDat": [{"x":0,"y":0}, ...],
  "huongNha": 135.0,
  "cuaChinh": {"x":3.2,"y":-8.1},
  "doVat": [
    {"id":12, "loai":"cong", "tam":{"x":1.5,"y":-10.0},
     "huong":180.0, "rong":2.95, "sau":0.10,
     "goc4":[{"x":..,"y":..}, ...]}
  ]
}
```

- **`huong`** lấy từ `transform.eulerAngles.y`. Trong Unity (trục Y hướng lên, hệ tay trái), số này đã là góc la bàn: 0 = Bắc, 90 = Đông, tính theo chiều kim đồng hồ. Không cần tính từ quaternion.
- **`tam.y = -position.z`** chỉ được tính ở một chỗ duy nhất trong C#, có unit test đi kèm.
- **`rong` và `sau`** là kích thước thật bằng mét (kích thước cơ sở × scale).
- **`goc4`** là bốn góc chân đế sau khi xoay. Nhờ vậy, các kiểm tra như "trước rộng sau hẹp" hay chồng lấn không phải dựng lại hình học.
- **Các trường cũ vẫn giữ** để tải lại blueprint, và cả hai định dạng nằm chung một file.

### Bước 5: Cảnh kiểm tra cái bẫy toạ độ

Đặt một cổng cách tâm nhà đúng 10 m về phía Đông. Kết quả xuất ra phải cho hướng ≈ 90°, `x = +10`, `y = 0`. Nếu Đông và Tây bị đổi chỗ thì thấy ngay.

## Sửa công thức yaw

Công thức `yaw = atan2(2(w·y + x·z), 1 − 2(y² + z²))` dùng cho hệ trục Z hướng lên. Với Unity, công thức đúng là:

```
yaw = atan2(2(w·y + x·z), 1 − 2(x² + y²))
```

Hiện tại công cụ xoay chỉ xoay quanh trục đứng, nên hai công thức cho cùng kết quả. Nhưng khi mô hình có độ nghiêng thì kết quả lệch nhau. Đây cũng là kiểu lỗi âm thầm giống lỗi đổi Đông với Tây. Bước 4 tránh được lỗi này vì xuất thẳng góc ra.

## Gợi ý cho phía AI

1. **Để một rule engine cố định tính hình học, không để Gemini tính.** Hướng, cung nào trong 8 cung, khoảng cách (công cụ đo đã tính từ tâm đến tâm) và hình dạng thửa đất đều nên tính bằng code. Gemini chỉ nhận kết quả, ví dụ "cổng ở hướng Đông Nam, cách cửa chính 6,2 m", rồi giải thích hoặc đánh giá. Mô hình ngôn ngữ thường tính sai với góc và toạ độ.
2. **Yêu cầu Gemini trả về theo cấu trúc cố định (structured output).** Đưa cho nó một JSON schema, trong đó danh sách nhãn lỗi là cố định (`maLoi`, `mucDo`, `doVatLienQuan[]`, `giaiThich`). Như vậy câu trả lời có thể so tự động với nhãn.
3. **Tạo dữ liệu huấn luyện tự động từ Unity.** Unity có thể đặt mô hình ngẫu nhiên trên các thửa đất ngẫu nhiên rồi xuất ra. Sau đó rule engine tự gán nhãn lỗi đúng, nên có rất nhiều mẫu đã gán nhãn mà không phải làm tay. Bộ dữ liệu gán nhãn tay thì giữ lại để kiểm tra.
4. **Dùng tên trường khác nhau trong engine.** Cùng chữ `y` mà bên Unity là chiều cao, bên engine là toạ độ mặt đất. Trong engine nên đặt tên toạ độ mặt đất kiểu `dong` và `nam`. Khi đó adapter mà nhầm sẽ báo lỗi ngay, thay vì âm thầm cho kết quả sai.
5. **Các phần trong nhà mà app chưa mô hình hoá.** Những quy tắc liên quan đến phòng như bếp chưa kiểm tra được, vì mỗi ngôi nhà là một khối liền. Có hai cách: tạm bỏ các quy tắc đó, hoặc cho người dùng chấm một điểm "bếp" trên nhà.

## Cần cung cấp

- **Danh sách nhãn trong bộ dữ liệu:** chính xác các giá trị `loai` và các mã lỗi, để Bước 1 khớp theo đó thay vì dùng danh sách đoán ở trên.
- **Thứ tự làm.** Đề xuất làm Bước 1 và Bước 4 trước, vì ít tốn công và loại bỏ được cái bẫy toạ độ. Bước 2 cần nhìn xác nhận mặt tiền từng nhà. Bước 3 cần thêm công cụ vẽ ranh đất. Tất cả đều là thay đổi C#, Unity chỉ cần biên dịch lại. Riêng việc nạp giá trị `loai` vào catalog thì tốt nhất đóng Unity để chạy tự động, hoặc tự bấm một mục menu.
