namespace ProductionManagement.Domain.Entities;

/// <summary>Ảnh mẫu của đơn hàng. Bốn thuộc tính đi cùng nhau: có tất cả, hoặc không có gì.</summary>
public readonly record struct OrderImage(string Path, string FileName, string ContentType, int SizeBytes);

/// <summary>Một dây chuyền trong request lập tiến độ: phân bổ tầng 1 kèm phân bổ tầng 2 của nó.</summary>
public readonly record struct ScheduleLine(
    Guid ProductionLineId,
    int AllocatedQuantity,
    IReadOnlyList<(DateOnly ProductionDate, int PlannedQuantity)> Plans);

/// <summary>
/// Aggregate root của quản lý sản xuất (Step 1 §3).
/// Các giá trị suy ra (TotalActual, Remaining, Progress, TotalPlan) không bao giờ được lưu ở đây.
///
/// Sau CR-001, đơn hàng có hai thời điểm tách biệt:
///   Nhập hàng   — mã giày + số lượng + tối đa 1 ảnh. Trạng thái <c>Pending</c>, chưa có ngày,
///                 chưa có dây chuyền, chưa có kế hoạch.
///   Lập tiến độ — chọn dây chuyền, khoảng ngày và phân bổ 2 tầng. Chuyển sang <c>Incomplete</c>.
/// Lập tiến độ là thao tác một lần; đơn không bao giờ quay lại <c>Pending</c> (CR-001 §4.1, BR-N05).
/// </summary>
public sealed class Order
{
    private readonly List<ProductionPlan> _productionPlans = [];
    private readonly List<ProductionDay> _productionDays = [];
    private readonly List<OrderProductionLine> _productionLines = [];

    private Order() { }

    public Guid Id { get; private set; }

    /// <summary>Mã giày thay luôn mã đơn hàng, unique toàn hệ thống (CR-001 §2 QĐ-4, BR-N01).</summary>
    public string ShoeCode { get; private set; } = null!;

    public int Quantity { get; private set; }

    // Mỗi đơn tối đa 1 ảnh, lưu thẳng trên orders chứ không tách bảng (CR-001 §2 QĐ-5).
    // DB chỉ giữ đường dẫn tương đối; file nằm trên disk của server (QĐ-6).
    public string? ImagePath { get; private set; }
    public string? ImageFileName { get; private set; }
    public string? ImageContentType { get; private set; }
    public int? ImageSizeBytes { get; private set; }

    /// <summary>Thuộc về tiến độ, nên null khi đơn còn <c>Pending</c> (CR-001 §2 QĐ-8, BR-N04).</summary>
    public DateOnly? StartDate { get; private set; }

    public DateOnly? DueDate { get; private set; }
    public OrderStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<ProductionPlan> ProductionPlans => _productionPlans;
    public IReadOnlyCollection<ProductionDay> ProductionDays => _productionDays;
    public IReadOnlyCollection<OrderProductionLine> ProductionLines => _productionLines;

    public bool IsCompleted => Status == OrderStatus.Completed;

    /// <summary>Đã lập tiến độ hay chưa. Đơn chưa lập tiến độ không có ngày, dây chuyền, kế hoạch.</summary>
    public bool IsScheduled => Status != OrderStatus.Pending;

    public bool HasImage => ImagePath is not null;

    /// <summary>
    /// Đơn hàng đã qua ngày kết thúc mà chưa hoàn thành. Đây là cờ "trễ" hiển thị cho quản lý —
    /// đơn đã giao đủ thì không trễ, dù ngày kết thúc đã qua bao lâu đi nữa.
    /// Suy ra từ ngày kết thúc và trạng thái, không bao giờ được lưu xuống.
    ///
    /// Đơn chưa lập tiến độ chưa có ngày kết thúc nên không bao giờ trễ:
    /// chưa cam kết thì chưa lỡ hẹn.
    /// </summary>
    public static bool IsOverdue(OrderStatus status, DateOnly? dueDate, DateOnly today)
        => status != OrderStatus.Completed && dueDate is not null && dueDate < today;

    /// <inheritdoc cref="IsOverdue(OrderStatus, DateOnly?, DateOnly)"/>
    public bool IsOverdueOn(DateOnly today) => IsOverdue(Status, DueDate, today);

    /// <summary>
    /// Kỳ sản xuất đã kết thúc. Chủ đích độc lập với trạng thái: đơn đã giao đủ thì vẫn là đã qua
    /// ngày kết thúc, và vẫn bị đóng băng y như vậy. Bản thân ngày kết thúc vẫn tính là nằm trong
    /// kỳ — thực tế của ngày đó được nhập vào cuối ngày.
    /// </summary>
    public static bool IsPastDueDate(DateOnly? dueDate, DateOnly today) => dueDate is not null && dueDate < today;

    /// <inheritdoc cref="IsPastDueDate(DateOnly?, DateOnly)"/>
    public bool IsPastDueDateOn(DateOnly today) => IsPastDueDate(DueDate, today);

    /// <summary>
    /// Nhập hàng: hàng về, biết mã giày và số lượng cần làm. Chưa biết chạy dây chuyền nào, chưa
    /// biết bắt đầu ngày nào (CR-001 §4.1).
    /// </summary>
    public static Order Receive(string? shoeCode, int quantity, DateTimeOffset now)
    {
        var normalized = ValidateReceipt(shoeCode, quantity);

        return new Order
        {
            Id = Guid.CreateVersion7(),
            ShoeCode = normalized,
            Quantity = quantity,
            Status = OrderStatus.Pending,
            StartDate = null,
            DueDate = null,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>
    /// Sửa thông tin nhập hàng. Số lượng chỉ đổi được khi chưa lập tiến độ: sau đó nó đã là mốc đối
    /// chiếu của phân bổ hai tầng, đổi một mình nó sẽ phá bất biến BR-N08.
    /// </summary>
    public void UpdateReceipt(string? shoeCode, int quantity, DateTimeOffset now)
    {
        var normalized = ValidateReceipt(shoeCode, quantity);

        if (IsScheduled && quantity != Quantity)
        {
            throw new BusinessRuleException(
                ErrorCodes.OrderScheduleAlreadyExists,
                "This order already has a production schedule, so its quantity can no longer be changed.");
        }

        ShoeCode = normalized;
        Quantity = quantity;
        UpdatedAt = now;
    }

    /// <summary>
    /// Chỉ đơn chưa lập tiến độ mới xoá được. Lúc đó đơn chưa có kế hoạch, dây chuyền hay sản lượng
    /// nào, nên xoá cứng không làm mất lịch sử sản xuất — luật "không hard-delete đơn hàng khi đã có
    /// dữ liệu sản xuất" (Step 3 §7) vẫn nguyên. Đã lập tiến độ thì không xoá được nữa.
    /// </summary>
    public void EnsureDeletable()
    {
        if (IsScheduled)
        {
            throw new BusinessRuleException(
                ErrorCodes.OrderNotDeletable,
                $"Order '{ShoeCode}' already has a production schedule, so it can no longer be deleted.");
        }
    }

    /// <summary>Gắn ảnh mẫu. Đơn chỉ có tối đa 1 ảnh nên ảnh mới luôn thay ảnh cũ (BR-N02).</summary>
    /// <returns>Đường dẫn của ảnh cũ, để bên gọi xoá file sau khi transaction đã commit.</returns>
    public string? AttachImage(OrderImage image, DateTimeOffset now)
    {
        var previousPath = ImagePath;

        ImagePath = image.Path;
        ImageFileName = image.FileName;
        ImageContentType = image.ContentType;
        ImageSizeBytes = image.SizeBytes;
        UpdatedAt = now;

        return previousPath;
    }

    /// <returns>Đường dẫn của ảnh vừa gỡ, để bên gọi xoá file sau khi transaction đã commit.</returns>
    public string RemoveImage(DateTimeOffset now)
    {
        var previousPath = ImagePath
            ?? throw new NotFoundException(ErrorCodes.ImageNotFound, "This order has no image.");

        ImagePath = null;
        ImageFileName = null;
        ImageContentType = null;
        ImageSizeBytes = null;
        UpdatedAt = now;

        return previousPath;
    }

    /// <summary>
    /// Lập tiến độ: chốt dây chuyền, khoảng thời gian và phân bổ hai tầng
    /// (đơn → dây chuyền → ngày). Thao tác một lần, chỉ áp dụng cho đơn <c>Pending</c> (BR-N05).
    ///
    /// Bên gọi chịu trách nhiệm kiểm dây chuyền có tồn tại và đang Active hay không — đó là dữ liệu
    /// ngoài aggregate. Mọi bất biến về con số nằm ở đây.
    /// </summary>
    public void Schedule(DateOnly startDate, DateOnly dueDate, IReadOnlyList<ScheduleLine> lines, DateTimeOffset now)
    {
        if (IsScheduled)
        {
            throw new ConflictException(
                ErrorCodes.OrderScheduleAlreadyExists,
                $"Order '{ShoeCode}' already has a production schedule.");
        }

        var failures = new List<ValidationFailure>();

        if (startDate > dueDate)
        {
            failures.Add(new ValidationFailure("dueDate", "DUE_DATE_BEFORE_START_DATE", "Due date must be on or after the start date."));
        }

        if (lines.Count == 0)
        {
            failures.Add(new ValidationFailure("lines", "REQUIRED", "At least one production line is required."));
        }

        var seenLines = new HashSet<Guid>();
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];

            if (!seenLines.Add(line.ProductionLineId))
            {
                failures.Add(new ValidationFailure(
                    $"lines[{i}].productionLineId", "DUPLICATE_PRODUCTION_LINE",
                    "Each production line can only appear once."));
            }

            var seenDates = new HashSet<DateOnly>();
            for (var j = 0; j < line.Plans.Count; j++)
            {
                var (date, planned) = line.Plans[j];

                if (planned < 0)
                {
                    failures.Add(new ValidationFailure(
                        $"lines[{i}].plans[{j}].plannedQuantity", "MUST_BE_GREATER_THAN_OR_EQUAL_TO_ZERO",
                        "Planned quantity cannot be negative."));
                }

                if (date < startDate || date > dueDate)
                {
                    failures.Add(new ValidationFailure(
                        $"lines[{i}].plans[{j}].productionDate", "OUT_OF_PRODUCTION_PERIOD",
                        "Production date must fall inside the production period."));
                }

                if (!seenDates.Add(date))
                {
                    failures.Add(new ValidationFailure(
                        $"lines[{i}].plans[{j}].productionDate", "DUPLICATE_PRODUCTION_DATE",
                        "Each production date can only appear once per production line."));
                }
            }
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        // Dây chuyền được chọn thì phải được phân bổ; chọn rồi để trống là lỗi nhập liệu, không phải
        // "dây chuyền nghỉ cả kỳ" (BR-N07).
        var empty = lines.Where(l => l.AllocatedQuantity <= 0).ToList();
        if (empty.Count > 0)
        {
            throw new BusinessRuleException(
                ErrorCodes.ProductionLineEmptyAllocation,
                "Every selected production line must be allocated a quantity greater than zero.",
                empty.Select(l => new ValidationFailure(
                    l.ProductionLineId.ToString(), "MUST_BE_GREATER_THAN_ZERO",
                    l.AllocatedQuantity.ToString())).ToList());
        }

        // Tầng 1 — tổng phân bổ cho các dây chuyền phải bằng đúng số lượng đơn (BR-N08).
        var totalAllocated = lines.Sum(l => (long)l.AllocatedQuantity);
        if (totalAllocated != Quantity)
        {
            throw new BusinessRuleException(
                ErrorCodes.LineAllocationMismatch,
                $"The total allocated quantity ({totalAllocated}) must equal the order quantity ({Quantity}).");
        }

        // Tầng 2 — với mỗi dây chuyền, tổng kế hoạch theo ngày phải bằng số đã phân cho nó (BR-N08b).
        // details chỉ rõ dây chuyền nào lệch và lệch bao nhiêu, vì UI phải tô đỏ đúng cột.
        var mismatched = lines
            .Select(l => (Line: l, Total: l.Plans.Sum(p => (long)p.PlannedQuantity)))
            .Where(x => x.Total != x.Line.AllocatedQuantity)
            .ToList();

        if (mismatched.Count > 0)
        {
            throw new BusinessRuleException(
                ErrorCodes.LinePlanTotalMismatch,
                "For each production line, the total daily plan must equal the quantity allocated to it.",
                mismatched.Select(x => new ValidationFailure(
                    x.Line.ProductionLineId.ToString(), "LINE_PLAN_TOTAL_MISMATCH",
                    $"{x.Total}/{x.Line.AllocatedQuantity}")).ToList());
        }

        StartDate = startDate;
        DueDate = dueDate;
        Status = OrderStatus.Incomplete;
        UpdatedAt = now;

        foreach (var line in lines)
        {
            _productionLines.Add(OrderProductionLine.Create(this, line.ProductionLineId, line.AllocatedQuantity, now));

            // Ô bằng 0 nghĩa là dây chuyền đó nghỉ ngày đó, và cố ý KHÔNG tạo dòng plan: ô không có
            // kế hoạch thì không nhập được sản lượng, nhất quán với BR-N10 (CR-001 §6.6b, §7.6).
            foreach (var (date, planned) in line.Plans.Where(p => p.PlannedQuantity > 0).OrderBy(p => p.ProductionDate))
            {
                _productionPlans.Add(ProductionPlan.Create(this, line.ProductionLineId, date, planned, now));
            }
        }
    }

    /// <summary>
    /// Trạng thái đơn hàng suy ra từ tổng sản lượng thực tế và không bao giờ do quản lý đặt
    /// (Step 1 §13).
    ///
    /// Chỉ được đánh giá tại đúng một thời điểm: khi Xuất hàng một ô sản xuất (CR-01 OV-4, §14.1).
    /// Ghi nhận sản lượng trong ngày không đụng tới trạng thái đơn — tổng thực tế bằng số lượng đơn
    /// mà chưa Xuất hàng thì đơn vẫn Incomplete. Phải gọi bên trong đúng transaction đóng ô.
    /// </summary>
    public void RecalculateStatus(int totalActual, DateTimeOffset now)
    {
        // Đơn chưa lập tiến độ không có sản lượng nào để đánh giá, và không bao giờ rời Pending bằng
        // con đường này — chỉ Schedule() mới đưa nó ra khỏi Pending (CR-001 §4.1).
        if (!IsScheduled)
        {
            return;
        }

        var newStatus = totalActual >= Quantity ? OrderStatus.Completed : OrderStatus.Incomplete;
        if (newStatus == Status)
        {
            return;
        }

        Status = newStatus;
        UpdatedAt = now;
    }

    private static string ValidateReceipt(string? shoeCode, int quantity)
    {
        var failures = new List<ValidationFailure>();

        shoeCode = shoeCode?.Trim() ?? string.Empty;
        if (shoeCode.Length == 0)
        {
            failures.Add(new ValidationFailure("shoeCode", "REQUIRED", "Shoe code is required."));
        }
        else if (shoeCode.Length > 50)
        {
            failures.Add(new ValidationFailure("shoeCode", "MAX_LENGTH_EXCEEDED", "Shoe code must be at most 50 characters."));
        }

        if (quantity <= 0)
        {
            failures.Add(new ValidationFailure("quantity", "MUST_BE_GREATER_THAN_ZERO", "Quantity must be greater than zero."));
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        return shoeCode;
    }
}
