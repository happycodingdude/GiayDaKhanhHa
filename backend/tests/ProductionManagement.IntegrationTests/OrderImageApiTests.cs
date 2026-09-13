using System.Net;
using Xunit;

namespace ProductionManagement.IntegrationTests;

/// <summary>
/// Ảnh mẫu của đơn hàng (CR-001 Slice 2, BR-N02/N03).
///
/// Định dạng được nhận diện bằng chữ ký byte, nên các test này chỉ cần đúng vài byte đầu — phần
/// thân file là gì không quan trọng, và đó chính là điều cần chứng minh: một file PDF khai báo
/// <c>image/jpeg</c> vẫn bị chặn.
/// </summary>
public class OrderImageApiTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    private static (byte[], string, string) Jpeg(int sizeBytes = 1024)
    {
        var bytes = new byte[sizeBytes];
        bytes[0] = 0xFF;
        bytes[1] = 0xD8;
        bytes[2] = 0xFF;
        bytes[3] = 0xE0;
        return (bytes, "mau.jpg", "image/jpeg");
    }

    private static (byte[], string, string) Png()
    {
        var bytes = new byte[512];
        byte[] signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        signature.CopyTo(bytes, 0);
        return (bytes, "mau.png", "image/png");
    }

    /// <summary>File PDF nhưng khai báo là ảnh — đúng kiểu tấn công mà việc sniff byte chặn được.</summary>
    private static (byte[], string, string) PdfDisguisedAsJpeg()
    {
        var bytes = new byte[512];
        "%PDF-1.4"u8.ToArray().CopyTo(bytes, 0);
        return (bytes, "tailieu.jpg", "image/jpeg");
    }

    [Fact]
    public async Task An_order_can_be_received_together_with_its_image()
    {
        var client = await ClientAsync();

        var order = await ReceiveOrderAsync(client, 1000, image: Jpeg());

        Assert.True(order.HasImage);
        Assert.Equal($"/api/v1/orders/{order.Id}/image/content", order.ImageUrl);

        var content = await client.GetAsync(order.ImageUrl);
        content.EnsureSuccessStatusCode();
        Assert.Equal("image/jpeg", content.Content.Headers.ContentType?.MediaType);
        Assert.Equal(1024, (await content.Content.ReadAsByteArrayAsync()).Length);
    }

    [Fact]
    public async Task A_pdf_renamed_as_a_jpg_is_rejected()
    {
        var client = await ClientAsync();

        var response = await PostReceiptAsync(client, NextShoeCode(), "100", PdfDisguisedAsJpeg());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.ReadErrorAsync();
        Assert.Equal("VALIDATION_ERROR", error.Code);
        Assert.Contains(error.Details!, d => d.Code == "IMAGE_TYPE_NOT_SUPPORTED");
    }

    [Fact]
    public async Task An_image_larger_than_five_megabytes_is_rejected()
    {
        var client = await ClientAsync();

        var response = await PostReceiptAsync(client, NextShoeCode(), "100", Jpeg(5 * 1024 * 1024 + 1));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains((await response.ReadErrorAsync()).Details!, d => d.Code == "IMAGE_TOO_LARGE");
    }

    [Fact]
    public async Task Replacing_the_image_makes_the_old_file_unreachable()
    {
        var client = await ClientAsync();
        var order = await ReceiveOrderAsync(client, 100, image: Jpeg());

        var first = await (await client.GetAsync(order.ImageUrl)).Content.ReadAsByteArrayAsync();

        var form = new MultipartFormDataContent();
        var (bytes, fileName, contentType) = Png();
        var part = new ByteArrayContent(bytes);
        part.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        form.Add(part, "image", fileName);

        var replaced = await client.PutAsync($"/api/v1/orders/{order.Id}/image", form);
        replaced.EnsureSuccessStatusCode();

        var after = await client.GetAsync(order.ImageUrl);
        after.EnsureSuccessStatusCode();

        // Cùng một URL nhưng nội dung đã là ảnh mới; ảnh cũ không còn truy cập được bằng cách nào.
        Assert.Equal("image/png", after.Content.Headers.ContentType?.MediaType);
        Assert.NotEqual(first.Length, (await after.Content.ReadAsByteArrayAsync()).Length);
    }

    [Fact]
    public async Task Deleting_the_image_leaves_the_order_without_one()
    {
        var client = await ClientAsync();
        var order = await ReceiveOrderAsync(client, 100, image: Jpeg());

        var deleted = await client.DeleteAsync($"/api/v1/orders/{order.Id}/image");
        deleted.EnsureSuccessStatusCode();

        Assert.False((await deleted.ReadAsync<OrderResponse>()).HasImage);

        var content = await client.GetAsync($"/api/v1/orders/{order.Id}/image/content");
        Assert.Equal(HttpStatusCode.NotFound, content.StatusCode);
        Assert.Equal("IMAGE_NOT_FOUND", (await content.ReadErrorAsync()).Code);
    }

    [Fact]
    public async Task Deleting_an_image_that_does_not_exist_returns_a_not_found_error()
    {
        var client = await ClientAsync();
        var order = await ReceiveOrderAsync(client, 100);

        var response = await client.DeleteAsync($"/api/v1/orders/{order.Id}/image");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("IMAGE_NOT_FOUND", (await response.ReadErrorAsync()).Code);
    }

    // --- Lưu thông tin + ảnh trong một lần (PUT /orders/{id}) ---------------------------------------

    [Fact]
    public async Task Saving_the_receipt_with_a_new_image_replaces_the_image_together_with_the_details()
    {
        var client = await ClientAsync();
        var order = await ReceiveOrderAsync(client, 100, image: Jpeg());
        var newCode = NextShoeCode();

        var response = await PutReceiptAsync(client, order.Id, newCode, 200, image: Png());

        response.EnsureSuccessStatusCode();
        var updated = await response.ReadAsync<OrderResponse>();
        Assert.Equal(newCode, updated.ShoeCode);
        Assert.Equal(200, updated.Quantity);
        Assert.True(updated.HasImage);

        var content = await client.GetAsync(updated.ImageUrl);
        content.EnsureSuccessStatusCode();
        Assert.Equal("image/png", content.Content.Headers.ContentType?.MediaType);

        // File cũ bị xoá sau khi commit: trên disk chỉ còn đúng ảnh mới.
        Assert.Single(Factory.ImageFilesOf(order.Id));
    }

    [Fact]
    public async Task Saving_the_receipt_with_remove_image_removes_the_image_and_its_file()
    {
        var client = await ClientAsync();
        var order = await ReceiveOrderAsync(client, 100, image: Jpeg());

        var response = await PutReceiptAsync(client, order.Id, order.ShoeCode, 100, removeImage: true);

        response.EnsureSuccessStatusCode();
        Assert.False((await response.ReadAsync<OrderResponse>()).HasImage);

        var content = await client.GetAsync($"/api/v1/orders/{order.Id}/image/content");
        Assert.Equal(HttpStatusCode.NotFound, content.StatusCode);
        Assert.Empty(Factory.ImageFilesOf(order.Id));
    }

    [Fact]
    public async Task A_save_that_fails_keeps_the_old_details_and_the_old_image()
    {
        var client = await ClientAsync();
        var taken = await ReceiveOrderAsync(client, 100);
        var order = await ReceiveOrderAsync(client, 100, image: Jpeg());

        // Mã giày trùng với đơn khác: cả thông tin lẫn ảnh mới đều không được lưu.
        var response = await PutReceiptAsync(client, order.Id, taken.ShoeCode, 300, image: Png());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("SHOE_CODE_ALREADY_EXISTS", (await response.ReadErrorAsync()).Code);

        var current = await (await client.GetAsync($"/api/v1/orders/{order.Id}")).ReadAsync<OrderResponse>();
        Assert.Equal(order.ShoeCode, current.ShoeCode);
        Assert.Equal(100, current.Quantity);

        var content = await client.GetAsync(current.ImageUrl);
        content.EnsureSuccessStatusCode();
        Assert.Equal("image/jpeg", content.Content.Headers.ContentType?.MediaType);

        // Ảnh mới không được để lại thành file mồ côi.
        Assert.Single(Factory.ImageFilesOf(order.Id));
    }

    [Fact]
    public async Task An_invalid_image_rejects_the_whole_save()
    {
        var client = await ClientAsync();
        var order = await ReceiveOrderAsync(client, 100);

        var response = await PutReceiptAsync(client, order.Id, NextShoeCode(), 100, image: PdfDisguisedAsJpeg());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains((await response.ReadErrorAsync()).Details!, d => d.Code == "IMAGE_TYPE_NOT_SUPPORTED");

        var current = await (await client.GetAsync($"/api/v1/orders/{order.Id}")).ReadAsync<OrderResponse>();
        Assert.Equal(order.ShoeCode, current.ShoeCode);
    }

    [Fact]
    public async Task A_new_image_and_remove_image_in_the_same_save_are_rejected()
    {
        var client = await ClientAsync();
        var order = await ReceiveOrderAsync(client, 100, image: Jpeg());

        var response = await PutReceiptAsync(client, order.Id, order.ShoeCode, 100, image: Png(), removeImage: true);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains((await response.ReadErrorAsync()).Details!, d => d.Code == "CONFLICTS_WITH_IMAGE");
        Assert.Single(Factory.ImageFilesOf(order.Id));
    }

    [Fact]
    public async Task Removing_the_image_of_an_order_without_one_is_a_no_op()
    {
        var client = await ClientAsync();
        var order = await ReceiveOrderAsync(client, 100);

        // PUT phải gửi lại được: lần gửi trước đã gỡ ảnh thì lần này không được biến thành lỗi 404.
        var response = await PutReceiptAsync(client, order.Id, order.ShoeCode, 100, removeImage: true);

        response.EnsureSuccessStatusCode();
        Assert.False((await response.ReadAsync<OrderResponse>()).HasImage);
    }

    [Fact]
    public async Task The_image_endpoint_requires_authentication()
    {
        var client = await ClientAsync();
        var order = await ReceiveOrderAsync(client, 100, image: Jpeg());

        // Client chưa đăng nhập: ảnh không được phục vụ cho người lạ (CR-001 §2 QĐ-6).
        var anonymous = Factory.CreateClient();
        var response = await anonymous.GetAsync($"/api/v1/orders/{order.Id}/image/content");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
