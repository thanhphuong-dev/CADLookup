# CADLookup

CADLookup là công cụ kiểm tra bản vẽ CAD đang hoạt động, chạy trong tiến trình (in-process) dưới dạng cửa sổ WPF. Các hợp đồng dùng chung và mô hình kiểm tra không tham chiếu trực tiếp đến CAD SDK; toàn bộ truy cập cơ sở dữ liệu CAD được thực hiện bởi một adapter máy chủ trong ngữ cảnh lệnh hợp lệ.

## Tính năng hiện tại

- Adapter **AutoCAD 2023** nhắm đến AutoCAD 2023 Managed SDK đã cài đặt và .NET Framework 4.8.
- Tab Ribbon **CADLookup** trong AutoCAD với các nút **Inspect** (Kiểm tra), **Select** (Chọn) và **Refresh** (Làm mới), cùng với lệnh `CADLOOKUP`.
- `CADLookup.Core` chứa các hợp đồng độc lập với SDK, ảnh chụp nhanh miền nghiệp vụ và dịch vụ ứng dụng kiểm tra; `CADLookup.Infrastructure` quản lý cài đặt cục bộ và ghi nhật ký file.
- Cửa sổ WPF `CADLOOKUP` dạng modeless (không chặn giao diện) được quản lý bởi cửa sổ chính AutoCAD, giao diện WPF thuần (không dùng thư viện UI ngoài) với theme sáng/tối, nhãn tiếng Việt/Anh, lưu tuỳ chọn ngôn ngữ, danh sách đối tượng, tra cứu handle, tìm kiếm thuộc tính/lọc nguồn, tab hình học, chi tiết tham chiếu đối tượng, sao chép giá trị/đường dẫn, xuất JSON/CSV và lịch sử điều hướng.
- Tìm kiếm dùng chung bộ so khớp token (mọi từ khoá phải xuất hiện), bỏ qua hoa/thường và dấu tiếng Việt: gõ `ban ve` vẫn tìm ra `Bản vẽ`; áp dụng cho lọc thuộc tính và lọc danh sách đối tượng.
- Phím tắt trong cửa sổ: `F5` làm mới, `Ctrl+F` nhảy tới ô tìm thuộc tính, `Ctrl+K` nhảy tới ô tra cứu handle, `Esc` xoá bộ lọc đang nhập.
- Thông tin đối tượng cố định bao gồm: định danh đối tượng, lớp/trạng thái, thuộc tính trực quan, kích thước đường thẳng/tròn/polyline/cung tròn/ellipse/spline/hatch, giá trị kích thước, văn bản, thuộc tính block, bản ghi lớp và bảng block, phạm vi bao quanh, liên kết owner/block và XData.
- Các thuộc tính CLR công khai được liệt kê dưới dạng thành viên trì hoãn. Nhấp đúp vào một thành viên để đánh giá nó trong ngữ cảnh lệnh máy chủ; các giá trị phức tạp và tập hợp có thể được mở lại. Chữ ký phương thức công khai và các loại sự kiện được lập danh mục nhưng không bao giờ được gọi hoặc đăng ký.
- Quá trình kiểm tra áp dụng giới hạn độ sâu, thuộc tính và tập hợp; các vòng lặp và getter thất bại được báo cáo là trạng thái thuộc tính thay vì kết thúc phiên.
- `CADLookup.Playground` sử dụng đồ thị đối tượng giả để kiểm tra các giá trị lồng nhau, danh sách, phương thức, sự kiện và tham chiếu vòng mà không cần khởi động CAD.
- `CADLookup.Core.Tests` bao gồm giới hạn, đường dẫn thành viên trì hoãn, phát hiện vòng lặp, lỗi từng thành viên và lập danh mục phương thức/sự kiện. `CADLookup.UI.Tests` bao gồm lọc, lịch sử điều hướng và định tuyến tra cứu handle.

> Đây chưa phải là bản tương đương đầy đủ với RevitLookup. CADLookup không gọi phương thức, không duyệt các bảng cơ sở dữ liệu CAD tuỳ ý, không chỉnh sửa đối tượng bản vẽ, không cung cấp tìm kiếm toàn cơ sở dữ liệu theo thời gian thực, và không triển khai bộ trực quan hóa hình học. Các adapter ZWCAD và enjiCAD chưa được cung cấp: mỗi loại yêu cầu SDK/runtime build riêng và xác minh trên máy chủ thực tế.

## Build, kiểm tra và cài đặt

Yêu cầu: Windows với .NET SDK, .NET Framework 4.8 targeting pack và AutoCAD 2023 đã cài đặt. Các assembly AutoCAD SDK được tham chiếu từ thư mục cài đặt và không được sao chép vào bundle.

```powershell
dotnet test .\CADLookup.sln -c Release -p:AutoCADManagedDir="C:\Program Files\Autodesk\AutoCAD 2023"
.\build-bundle.ps1 -AutoCADManagedDir "C:\Program Files\Autodesk\AutoCAD 2023" -Install
```

Script build tạo ra `.artifacts\CADLookup.bundle`; tham số `-Install` sao chép bundle vào thư mục `ApplicationPlugins` của AutoCAD cho người dùng hiện tại. Khởi động lại AutoCAD, chọn một hoặc nhiều đối tượng bản vẽ, sau đó nhấp **CADLookup > Inspect** hoặc chạy lệnh `CADLOOKUP`.

- **Select** – Bắt đầu lệnh chọn đối tượng trong AutoCAD.
- **Refresh** – Tải lại lần kiểm tra hiện tại (hoặc nhấn `F5` trong cửa sổ).
- **Nhấp đúp** một hàng có tham chiếu điều hướng để kiểm tra nó.
- **Nhấp đúp** một thuộc tính reflection trì hoãn để đánh giá nó.
- **Nhấp chuột phải** vào thuộc tính để sao chép giá trị, sao chép đường dẫn và mở đối tượng liên quan.
- Nhập handle dạng thập lục phân vào thanh trên cùng để tìm đối tượng trong bản vẽ đang hoạt động.

Để build mà không cài đặt, bỏ qua tham số `-Install`. Để tải thủ công, sao chép bundle đã tạo vào `ApplicationPlugins`, hoặc dùng lệnh `NETLOAD` với file `CADLookup.Host.AutoCAD.dll` từ thư mục Release; giữ `CADLookup.Core.dll`, `CADLookup.UI.dll` và `CADLookup.Infrastructure.dll` cùng thư mục với nó.

## Kiểm tra smoke AutoCAD 2023

`tests/CADLookup.Host.AutoCAD.Smoke` là bộ kiểm tra tích hợp in-process và được build cùng với solution. Trong một bản vẽ AutoCAD chưa lưu dùng để thử nghiệm:

1. Sao chép `tests/CADLookup.Host.AutoCAD.Smoke/bin/Release/net48/CADLookup.Host.AutoCAD.Smoke.dll` vào thư mục `Contents/Windows` của bundle đã cài đặt.
2. Chạy lệnh `CADLOOKUP`, sau đó `NETLOAD` file dll vừa sao chép và chạy `CADLOOKUPSMOKE`.
3. Harness sẽ tạo và xác minh các đối tượng Line, Circle, Polyline, Text, Block có Attribute, Layer và đối tượng đã xóa; đồng thời xác minh các điều khiển Ribbon và cửa sổ WPF.
4. Kết quả PASS/FAIL được ghi vào `%TEMP%\CADLookup-AutoCAD-smoke.txt`.

## Trạng thái hỗ trợ máy chủ CAD

| Máy chủ | Trạng thái | Ghi chú |
|---|---|---|
| AutoCAD 2023 | ✅ Đã xác minh (MVP) | `R24.2`, .NET Framework 4.8; build và smoke test in-host đã vượt qua với một đường thẳng (Line) được chọn trước. |
| AutoCAD 2024–2027 | ⏳ Chưa đóng gói | Thay đổi Runtime/API yêu cầu các build target và bundle manifest riêng biệt. |
| ZWCAD | 🔬 Nghiên cứu | Nhà cung cấp công bố các bộ phát triển .NET/ZRX theo từng phiên bản; API adapter và runtime cần một spike SDK chuyên dụng. |
| enjiCAD | 🔬 Nghiên cứu | Thông tin sản phẩm quảng bá phát triển .NET/GRX; các assembly API managed, runtime, mô hình tải và điều khoản phân phối chưa được xác minh. |

> **Lưu ý:** Không phát hành adapter máy chủ mới cho đến khi các hành vi về chọn đối tượng, transaction, ngữ cảnh tài liệu, giao diện modeless, định danh đối tượng và đóng gói đã được kiểm tra trong sản phẩm CAD đó.

Xem [ma trận xác minh máy chủ chi tiết](docs/Host-support-matrix.md) để biết bằng chứng và các câu hỏi SDK/runtime chưa được giải quyết.
