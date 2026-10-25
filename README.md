# CADLookup

CADLookup là công cụ kiểm tra bản vẽ CAD đang hoạt động, chạy trong tiến trình (in-process) dưới dạng cửa sổ WPF. Các hợp đồng dùng chung và mô hình kiểm tra không tham chiếu trực tiếp đến CAD SDK; toàn bộ truy cập cơ sở dữ liệu CAD được thực hiện bởi một adapter máy chủ trong ngữ cảnh lệnh hợp lệ.

## Tính năng hiện tại

- Adapter **AutoCAD 2023** nhắm đến AutoCAD 2023 Managed SDK đã cài đặt và .NET Framework 4.8.
- Adapter **VinaCAD 2026** nhắm đến managed SDK .NET 8 đi kèm VinaCAD; dùng API Prima.VinaCAD/Teigha riêng, không dùng adapter AutoCAD.
- Adapter **ZWCAD 2026** dùng API ZWCAD riêng, target .NET Framework 4.8 và build thành thư mục NETLOAD độc lập.
- Tab Ribbon **CADLookup** trong AutoCAD với các nút **Inspect** (Kiểm tra), **Select** (Chọn), **Search** (Tìm kiếm), **Snoop DB** (Duyệt database) và **Refresh** (Làm mới), cùng với lệnh `CADLOOKUP`.
- **Tìm kiếm toàn bản vẽ** (lấy cảm hứng từ "Search Elements" của RevitLookup): hộp thoại riêng quét model space + space hiện tại theo handle, loại DXF, layer, tên block, tag/giá trị attribute và nội dung văn bản. Nhiều từ khóa là điều kiện AND, bỏ qua hoa/thường và dấu tiếng Việt (`ban ve` khớp `Bản vẽ`). Mở bằng nút **Search** trên thanh công cụ, `Ctrl+Shift+F`, menu chuột phải của vùng vẽ hoặc lệnh `CADLOOKUPFIND`; nhấp đúp vào kết quả để tra cứu chi tiết.
- **Chọn/Zoom đối tượng trong bản vẽ** (lấy cảm hứng từ phím F6/F7 của RevitLookup): `F6` bôi đen đối tượng đang tra cứu (pickfirst selection), `F7` zoom view tới phạm vi bao quanh của đối tượng — áp dụng cho đối tượng gốc hoặc bất kỳ tham chiếu liên quan nào qua menu chuột phải của hàng thuộc tính. Lệnh tương ứng: `CADLOOKUPMARK`, `CADLOOKUPZOOM`.
- **Gạch sáng tạm thời (F8)**: bật/tắt highlight trên đối tượng đang tra cứu mà không đổi selection và không ghi vào database (bỏ đi khi REGEN). Lệnh `CADLOOKUPHLIGHT`; tự tắt khi đổi bản vẽ hoặc đóng cửa sổ inspector.
- `CADLookup.Core` chứa các hợp đồng độc lập với SDK, ảnh chụp nhanh miền nghiệp vụ và dịch vụ ứng dụng kiểm tra; `CADLookup.Infrastructure` quản lý cài đặt cục bộ và ghi nhật ký file.
- Cửa sổ WPF `CADLOOKUP` dạng modeless (không chặn giao diện) dùng chung giữa các host, có theme sáng/tối, nhãn tiếng Việt/Anh, danh sách đối tượng, tra cứu handle, tìm kiếm/lọc thuộc tính, hình học, tham chiếu đối tượng, sao chép, xuất JSON/CSV và lịch sử điều hướng. Giao diện dùng HandyControl.
- **Dashboard "Bắt đầu"**: khi chưa tra cứu đối tượng nào, panel phải hiển thị các nút tác vụ nhanh (Chọn, Duyệt DB, Tìm kiếm) kèm cheat sheet lệnh.
- **Bảng thuộc tính nhóm theo nguồn** (bật/tắt được, nhớ lựa chọn): mỗi nhóm hiện badge đếm số thuộc tính; tooltip của hàng hiển thị giá trị đầy đủ, kiểu, trạng thái và đường dẫn thành viên.
- **Menu chuột phải trên cây đối tượng**: tra cứu, chọn/zoom trong bản vẽ và sao chép handle; **menu ≡** cạnh nút Back/Forward liệt kê các đối tượng đã xem để quay lại nhanh.
- **Cửa sổ inspector nhớ vị trí và kích thước** giữa các phiên (có kiểm tra bounds nằm trong màn hình hiện có).
- Tìm kiếm dùng chung bộ so khớp token (mọi từ khoá phải xuất hiện), bỏ qua hoa/thường và dấu tiếng Việt: gõ `ban ve` vẫn tìm ra `Bản vẽ`; áp dụng cho lọc thuộc tính và lọc danh sách đối tượng.
- Phím tắt trong cửa sổ: `F5` làm mới, `F6` chọn đối tượng trong bản vẽ, `F7` zoom tới đối tượng, `F8` bật/tắt gạch sáng, `Ctrl+Shift+F` mở hộp thoại tìm kiếm toàn bản vẽ, `Ctrl+F` nhảy tới ô tìm thuộc tính, `Ctrl+K` nhảy tới ô tra cứu handle, `Esc` xoá bộ lọc đang nhập.
- Thông tin đối tượng cố định bao gồm: định danh đối tượng, lớp/trạng thái, thuộc tính trực quan, kích thước đường thẳng/tròn/polyline/cung tròn/ellipse/spline/hatch, giá trị kích thước, văn bản, thuộc tính block, bản ghi lớp và bảng block, phạm vi bao quanh, liên kết owner/block và XData.
- **Xem trước hình học**: tab Hình học vẽ đối tượng đang tra cứu lên canvas WPF tự co giãn (trục Y hướng lên đúng theo bản vẽ). Line/Circle/Arc/Ellipse/Polyline/Spline/Point được vẽ đường chính xác; mọi đối tượng hình học khác hiển thị hình chữ nhật phạm vi bao. Dữ liệu là `GeometryFigure` host-neutral trong snapshot, dùng được cho cả 3 host.
- Các thuộc tính CLR công khai được liệt kê dưới dạng thành viên trì hoãn. Nhấp đúp vào một thành viên để đánh giá nó trong ngữ cảnh lệnh máy chủ; các giá trị phức tạp và tập hợp có thể được mở lại. Chữ ký phương thức công khai và các loại sự kiện được lập danh mục nhưng không bao giờ được gọi hoặc đăng ký.
- Quá trình kiểm tra áp dụng giới hạn độ sâu, thuộc tính và tập hợp; các vòng lặp và getter thất bại được báo cáo là trạng thái thuộc tính thay vì kết thúc phiên.
- `CADLookup.Playground` sử dụng đồ thị đối tượng giả để kiểm tra các giá trị lồng nhau, danh sách, phương thức, sự kiện và tham chiếu vòng mà không cần khởi động CAD.
- `CADLookup.Core.Tests` bao gồm giới hạn, đường dẫn thành viên trì hoãn, phát hiện vòng lặp, lỗi từng thành viên và lập danh mục phương thức/sự kiện. `CADLookup.UI.Tests` bao gồm lọc, lịch sử điều hướng và định tuyến tra cứu handle.

> Đây chưa phải là bản tương đương đầy đủ với RevitLookup. CADLookup không gọi phương thức, không chỉnh sửa đối tượng. Adapter VinaCAD và ZWCAD đã build nhưng chưa smoke test trong phiên CAD thật. enjiCAD chưa có adapter.

## Build, kiểm tra và cài đặt

Yêu cầu build AutoCAD: Windows, .NET SDK, .NET Framework 4.8 targeting pack và AutoCAD 2023. Build toàn solution cần thêm SDK VinaCAD 2026 và ZWCAD references; SDK assemblies CAD được tham chiếu lúc build nhưng không chép vào gói.

```powershell
dotnet test .\CADLookup.sln -c Release -p:AutoCADManagedDir="C:\Program Files\Autodesk\AutoCAD 2023" -p:VinaCADManagedDir="C:\Program Files\VinaCAD\VinaCAD 2026"
.\build-bundle.ps1 -AutoCADManagedDir "C:\Program Files\Autodesk\AutoCAD 2023" -Install
```

Script build tạo ra `.artifacts\CADLookup.bundle`; tham số `-Install` sao chép bundle vào thư mục `ApplicationPlugins` của AutoCAD cho người dùng hiện tại. Khởi động lại AutoCAD, chọn một hoặc nhiều đối tượng bản vẽ, sau đó nhấp **CADLookup > Inspect** hoặc chạy lệnh `CADLOOKUP`.

Plug-in được nạp khi AutoCAD khởi động để tự tạo tab Ribbon CADLookup; trước đây bundle chỉ nạp sau khi gọi command nên tab không thể xuất hiện ngay khi mở CAD.

- **Select** – Bắt đầu lệnh chọn đối tượng trong AutoCAD.
- **Search** – Mở hộp thoại tìm kiếm đối tượng trong toàn bản vẽ (`CADLOOKUPFIND`); nhấn đúp hoặc `Enter` trên kết quả để tra cứu, `Esc` đóng hộp thoại.
- **Snoop DB** – Duyệt các bảng và từ điển của database bản vẽ.
- **Refresh** – Tải lại lần kiểm tra hiện tại (hoặc nhấn `F5` trong cửa sổ).
- **Nhấp đúp** một hàng có tham chiếu điều hướng để kiểm tra nó.
- **Nhấp đúp** một thuộc tính reflection trì hoãn để đánh giá nó.
- **Nhấp chuột phải** vào thuộc tính để sao chép giá trị, sao chép đường dẫn, chọn/zoom đối tượng trong bản vẽ và mở đối tượng liên quan.
- Nhập handle dạng thập lục phân vào thanh trên cùng để tìm đối tượng trong bản vẽ đang hoạt động.

Để build mà không cài đặt, bỏ qua tham số `-Install`. Để tải thủ công, sao chép bundle đã tạo vào `ApplicationPlugins`, hoặc dùng lệnh `NETLOAD` với file `CADLookup.Host.AutoCAD.dll` từ thư mục Release; giữ `CADLookup.Core.dll`, `CADLookup.UI.dll` và `CADLookup.Infrastructure.dll` cùng thư mục với nó.

Để build riêng cho VinaCAD 2026, cần cài VinaCAD 2026 và SDK .NET 8 của nó:

```powershell
.\build-vinacad.ps1 -VinaCADManagedDir "C:\Program Files\VinaCAD\VinaCAD 2026"
```

Thư mục `.artifacts\CADLookup.VinaCAD` chứa plug-in cùng dependencies của CADLookup/HandyControl; không chép managed SDK DLL của VinaCAD vào gói. Nạp `CADLookup.Host.VinaCAD.dll` theo workflow plugin của VinaCAD rồi chạy `CADLOOKUP`. Bản này đã compile trên máy phát triển với SDK VinaCAD 2026; cần smoke test trong VinaCAD thật để xác nhận ribbon, selection và cửa sổ modeless. Build AutoCAD dùng riêng `build-bundle.ps1`, không cần VinaCAD SDK.

Để build riêng cho ZWCAD 2026, project tự lấy bộ reference ZWCAD 2026 đã pin trong NuGet package `ZWCAD.NetApi`; nếu máy có bộ SDK ZWCAD local thì truyền đường dẫn qua tham số:

```powershell
.\build-zwcad.ps1
# hoặc với SDK local:
.\build-zwcad.ps1 -ZwcadManagedDir "C:\Duong\Dan\Toi\ZwSDK"
```

Thư mục `.artifacts\CADLookup.ZWCAD` chứa plugin và dependencies CADLookup/HandyControl, không chứa các DLL API của ZWCAD. Nạp `CADLookup.Host.ZWCAD.dll` bằng `NETLOAD`, sau đó mở bằng nút CADLookup trên Ribbon hoặc chạy `CADLOOKUP`. Do thư mục SDK hiện thiếu một số managed API references, build sẽ lấy đúng các reference ZWCAD 2026 từ package đã pin trong project; để chạy plugin vẫn cần cài ZWCAD 2026. Hiện chỉ xác nhận build, chưa xác nhận load/run trong ZWCAD.

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
| VinaCAD 2026 | 🧪 Build đã xác minh; cần in-host smoke | Adapter riêng .NET 8, command, selection, transaction/database, WPF modeless và Ribbon; chưa xác minh load/run trong phiên VinaCAD. |
| ZWCAD 2026 | 🧪 Build đã xác minh; cần in-host smoke | Adapter riêng .NET Framework 4.8, selection, transaction/database, WPF modeless, menu ngữ cảnh và Ribbon; cần ZWCAD 2026 cài trên máy để xác nhận load/run. |
| enjiCAD | 🔬 Nghiên cứu | Thông tin sản phẩm quảng bá phát triển .NET/GRX; các assembly API managed, runtime, mô hình tải và điều khoản phân phối chưa được xác minh. |

> **Lưu ý:** Không phát hành adapter máy chủ mới cho đến khi các hành vi về chọn đối tượng, transaction, ngữ cảnh tài liệu, giao diện modeless, định danh đối tượng và đóng gói đã được kiểm tra trong sản phẩm CAD đó.

Xem [ma trận xác minh máy chủ chi tiết](docs/Host-support-matrix.md) để biết bằng chứng và các câu hỏi SDK/runtime chưa được giải quyết.
