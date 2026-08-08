using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using sep490_be.Hubs;
using sep490_be.Models;
using sep490_be.Enums;
using sep490_be.Services.Interfaces;
using sep490_be.Helpers;

namespace sep490_be.Services.Implementations
{
    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly Microsoft.Extensions.Logging.ILogger<NotificationService> _logger;
        private readonly UserManager<IdentityUser> _userManager;

        public NotificationService(
            ApplicationDbContext dbContext, 
            IHubContext<NotificationHub> hubContext,
            Microsoft.Extensions.Logging.ILogger<NotificationService> logger,
            UserManager<IdentityUser> userManager)
        {
            _dbContext = dbContext;
            _hubContext = hubContext;
            _logger = logger;
            _userManager = userManager;
        }

        public async Task SendClassCreatedNotificationAsync(Class classEntity)
        {
            // 1. Prepare title and content
            string title = "Lớp học mới được tạo";
            string content = $"Lớp học {classEntity.Name} ({classEntity.Code}) đã được tạo mới.";

            await SaveAndBroadcastNotificationAsync(classEntity, title, content);
        }

        public async Task SendClassStatusChangedNotificationAsync(Class classEntity, int oldStatus, int newStatus)
        {
            string oldStatusStr = GetClassStatusString(oldStatus);
            string newStatusStr = GetClassStatusString(newStatus);

            // 1. Prepare title and content
            string title = "Cập nhật trạng thái lớp học";
            string content = $"Lớp học {classEntity.Name} ({classEntity.Code}) đã đổi trạng thái từ '{oldStatusStr}' sang '{newStatusStr}'.";

            await SaveAndBroadcastNotificationAsync(classEntity, title, content);
        }

        public async Task SendStudentsAddedToClassNotificationAsync(Class classEntity, List<int> newStudentIds)
        {
            if (newStudentIds == null || !newStudentIds.Any()) return;

            string title = "Bạn đã được thêm vào lớp học";
            string content = $"Bạn đã được đăng ký vào lớp học {classEntity.Name} ({classEntity.Code}).";

            try
            {
                // Save a single notification record for this event
                var notification = new Notification
                {
                    Title = title,
                    Content = content,
                    Status = (int)NotificationStatus.Unread,
                    ClassId = classEntity.Id,
                    TargetType = (int)NotificationTargetType.Class,
                    TargetId = classEntity.Id,
                    SentAt = DateTime.UtcNow,
                    Code = $"NOTIF-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}",
                    Name = title
                };

                _dbContext.Notifications.Add(notification);
                await _dbContext.SaveChangesAsync();

                // Resolve Identity UserNames for new students only
                var newStudentEmails = await _dbContext.Students
                    .Where(s => newStudentIds.Contains(s.Id) && !string.IsNullOrEmpty(s.Email))
                    .Select(s => s.Email!.Trim())
                    .ToListAsync();

                var recipientUserNames = new List<string>();
                foreach (var email in newStudentEmails)
                {
                    var identityUser = await _userManager.FindByEmailAsync(email);
                    if (identityUser?.UserName != null)
                        recipientUserNames.Add(identityUser.UserName.Trim().ToLowerInvariant());
                }

                var uniqueRecipients = recipientUserNames.Distinct().ToList();

                var payload = new
                {
                    id = notification.Id,
                    title = notification.Title,
                    content = notification.Content,
                    classId = classEntity.Id,
                    sentAt = notification.SentAt,
                    status = notification.Status
                };

                _logger.LogInformation("Broadcasting 'student added' notification for class {ClassId}. New student recipients: {Recipients}",
                    classEntity.Id, string.Join(", ", uniqueRecipients));

                // Send only to newly added students
                if (uniqueRecipients.Any())
                {
                    await _hubContext.Clients.Users(uniqueRecipients).SendAsync("ReceiveNotification", payload);

                    var userIds = await _dbContext.Users
                        .Where(u => uniqueRecipients.Contains(u.UserName!.ToLower()))
                        .Select(u => u.Id)
                        .ToListAsync();

                    var fcmTokens = await _dbContext.UserDeviceTokens
                        .Where(t => userIds.Contains(t.UserId))
                        .Select(t => t.FcmToken)
                        .ToListAsync();

                    await SendFcmNotificationAsync(fcmTokens, title, content, new Dictionary<string, string>
                    {
                        { "classId", classEntity.Id.ToString() },
                        { "sentAt", notification.SentAt.ToString("o") }
                    });
                }

                // Also notify admin group
                await _hubContext.Clients.Group(NotificationHub.AdminGroup).SendAsync("ReceiveNotification", payload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while broadcasting 'students added' notification for class {ClassId}", classEntity.Id);
            }
        }

        public async Task SendTeacherAssignedToClassNotificationAsync(Class classEntity, int teacherId)
        {
            string title = "Bạn đã được phân công dạy lớp học";
            string content = $"Bạn đã được phân công giảng dạy lớp học {classEntity.Name} ({classEntity.Code}).";

            try
            {
                var notification = new Notification
                {
                    Title = title,
                    Content = content,
                    Status = (int)NotificationStatus.Unread,
                    ClassId = classEntity.Id,
                    TargetType = (int)NotificationTargetType.Class,
                    TargetId = classEntity.Id,
                    SentAt = DateTime.UtcNow,
                    Code = $"NOTIF-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}",
                    Name = title
                };

                _dbContext.Notifications.Add(notification);
                await _dbContext.SaveChangesAsync();

                // Resolve teacher Identity UserName
                var teacher = await _dbContext.Teachers.FindAsync(teacherId);
                var recipientUserNames = new List<string>();
                if (teacher != null && !string.IsNullOrEmpty(teacher.Email))
                {
                    var identityUser = await _userManager.FindByEmailAsync(teacher.Email);
                    if (identityUser?.UserName != null)
                        recipientUserNames.Add(identityUser.UserName.Trim().ToLowerInvariant());
                }

                var payload = new
                {
                    id = notification.Id,
                    title = notification.Title,
                    content = notification.Content,
                    classId = classEntity.Id,
                    sentAt = notification.SentAt,
                    status = notification.Status
                };

                _logger.LogInformation("Broadcasting 'teacher assigned' notification for class {ClassId}. Teacher recipients: {Recipients}",
                    classEntity.Id, string.Join(", ", recipientUserNames));

                if (recipientUserNames.Any())
                {
                    await _hubContext.Clients.Users(recipientUserNames).SendAsync("ReceiveNotification", payload);

                    var userIds = await _dbContext.Users
                        .Where(u => recipientUserNames.Contains(u.UserName!.ToLower()))
                        .Select(u => u.Id)
                        .ToListAsync();

                    var fcmTokens = await _dbContext.UserDeviceTokens
                        .Where(t => userIds.Contains(t.UserId))
                        .Select(t => t.FcmToken)
                        .ToListAsync();

                    await SendFcmNotificationAsync(fcmTokens, title, content, new Dictionary<string, string>
                    {
                        { "classId", classEntity.Id.ToString() },
                        { "sentAt", notification.SentAt.ToString("o") }
                    });
                }

                // Also notify admin group
                await _hubContext.Clients.Group(NotificationHub.AdminGroup).SendAsync("ReceiveNotification", payload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while broadcasting 'teacher assigned' notification for class {ClassId}", classEntity.Id);
            }
        }

        public async Task SendExamCreatedNotificationAsync(Exam examEntity)
        {
            if (examEntity == null || !examEntity.ClassId.HasValue) return;

            var classEntity = await _dbContext.Classes
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == examEntity.ClassId.Value && !c.IsDeleted);

            if (classEntity == null) return;

            string title = "Bài kiểm tra mới";
            string content = $"Bạn có bài kiểm tra mới: '{examEntity.Title}' trong lớp {classEntity.Name}.";

            await SaveAndBroadcastNotificationAsync(classEntity, title, content);
        }

        public async Task SendHomeworkCreatedNotificationAsync(Homework homeworkEntity)
        {
            if (homeworkEntity == null) return;

            var classEntity = await _dbContext.Classes
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == homeworkEntity.ClassId && !c.IsDeleted);

            if (classEntity == null) return;

            string title = "Bài tập về nhà mới";
            string content = $"Bạn có bài tập về nhà mới: '{homeworkEntity.Title}' trong lớp {classEntity.Name}.";

            await SaveAndBroadcastNotificationAsync(classEntity, title, content);
        }

        private async Task SaveAndBroadcastNotificationAsync(Class classEntity, string title, string content)
        {
            try
            {
                // 1. Save Notification to database
                var notification = new Notification
                {
                    Title = title,
                    Content = content,
                    Status = (int)NotificationStatus.Unread,
                    ClassId = classEntity.Id,
                    TargetType = (int)NotificationTargetType.Class,
                    TargetId = classEntity.Id,
                    SentAt = DateTime.UtcNow,
                    Code = $"NOTIF-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}",
                    Name = title
                };

                _dbContext.Notifications.Add(notification);
                await _dbContext.SaveChangesAsync();

                // 2. Fetch recipient UserNames from AspNetUsers via email
                // SignalR uses NameUserIdProvider which maps to Identity UserName (lowercased)
                var recipientUserNames = new List<string>();

                // Get Teacher Identity UserName by email
                if (classEntity.TeacherId.HasValue)
                {
                    var teacher = await _dbContext.Teachers.FindAsync(classEntity.TeacherId.Value);
                    if (teacher != null && !string.IsNullOrEmpty(teacher.Email))
                    {
                        var identityUser = await _userManager.FindByEmailAsync(teacher.Email);
                        if (identityUser?.UserName != null)
                            recipientUserNames.Add(identityUser.UserName.Trim().ToLowerInvariant());
                    }
                }

                // Get Student Identity UserNames by email
                var studentEmails = await _dbContext.StudentClasses
                    .Where(sc => sc.ClassId == classEntity.Id)
                    .Include(sc => sc.Student)
                    .Where(sc => sc.Student != null && !string.IsNullOrEmpty(sc.Student.Email))
                    .Select(sc => sc.Student.Email!.Trim())
                    .ToListAsync();

                foreach (var email in studentEmails)
                {
                    var identityUser = await _userManager.FindByEmailAsync(email);
                    if (identityUser?.UserName != null)
                        recipientUserNames.Add(identityUser.UserName.Trim().ToLowerInvariant());
                }

                // Deduplicate
                var uniqueRecipients = recipientUserNames.Distinct().ToList();

                // 3. Send via SignalR
                var payload = new
                {
                    id = notification.Id,
                    title = notification.Title,
                    content = notification.Content,
                    classId = classEntity.Id,
                    sentAt = notification.SentAt,
                    status = notification.Status
                };
                _logger.LogInformation("Broadcasting notification for class {ClassId}. Title: {Title}. Recipients: {Recipients}", 
                    classEntity.Id, title, string.Join(", ", uniqueRecipients));

                // Send to direct users (Students & Teacher of this class)
                if (uniqueRecipients.Any())
                {
                    await _hubContext.Clients.Users(uniqueRecipients).SendAsync("ReceiveNotification", payload);

                    var userIds = await _dbContext.Users
                        .Where(u => uniqueRecipients.Contains(u.UserName!.ToLower()))
                        .Select(u => u.Id)
                        .ToListAsync();

                    var fcmTokens = await _dbContext.UserDeviceTokens
                        .Where(t => userIds.Contains(t.UserId))
                        .Select(t => t.FcmToken)
                        .ToListAsync();

                    await SendFcmNotificationAsync(fcmTokens, title, content, new Dictionary<string, string>
                    {
                        { "classId", classEntity.Id.ToString() },
                        { "sentAt", notification.SentAt.ToString("o") }
                    });
                }

                // Send to Admin/Center manager/Academic staff group
                await _hubContext.Clients.Group(NotificationHub.AdminGroup).SendAsync("ReceiveNotification", payload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while saving and broadcasting notification for class {ClassId}", classEntity.Id);
            }
        }

        private string GetClassStatusString(int status)
        {
            if (Enum.IsDefined(typeof(ClassStatus), status))
            {
                return ((ClassStatus)status).GetStringValue();
            }
            return status.ToString();
        }

        private async Task SendFcmNotificationAsync(List<string> tokens, string title, string content, Dictionary<string, string>? data = null)
        {
            if (tokens == null || !tokens.Any()) return;

            try
            {
                var messaging = FirebaseAdmin.Messaging.FirebaseMessaging.DefaultInstance;
                if (messaging == null)
                {
                    _logger.LogWarning("FirebaseMessaging instance is null. Firebase SDK might not be initialized.");
                    await FileLogger.LogErrorAsync(
                        "FCM WARNING",
                        "SendFcmNotificationAsync",
                        "",
                        "FirebaseMessaging instance is null. Firebase SDK might not be initialized. Please check firebase_service_account.json.");
                    return;
                }

                int successCount = 0;
                int failureCount = 0;
                var errors = new List<string>();

                foreach (var token in tokens)
                {
                    try
                    {
                        var message = new FirebaseAdmin.Messaging.Message()
                        {
                            Token = token,
                            Notification = new FirebaseAdmin.Messaging.Notification()
                            {
                                Title = title,
                                Body = content
                            },
                            Data = data
                        };

                        var response = await messaging.SendAsync(message);
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        failureCount++;
                        errors.Add($"Token: {token.Substring(0, Math.Min(10, token.Length))}..., Error: {ex.Message}");
                    }
                }

                _logger.LogInformation("Sent FCM push notifications. Success count: {SuccessCount}, Failure count: {FailureCount}", 
                    successCount, failureCount);

                if (failureCount > 0)
                {
                    await FileLogger.LogErrorAsync(
                        "FCM SEND FAILURE",
                        "SendFcmNotificationAsync",
                        "",
                        $"Success: {successCount}, Failure: {failureCount}\nDetails:\n{string.Join("\n", errors)}");
                }
                else
                {
                    await FileLogger.LogErrorAsync(
                        "FCM SEND SUCCESS",
                        "SendFcmNotificationAsync",
                        "",
                        $"Successfully sent to {successCount} devices.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending FCM push notifications.");
                await FileLogger.LogErrorAsync(
                    "FCM ERROR",
                    "SendFcmNotificationAsync",
                    "",
                    $"Exception Message: {ex.Message}\nStackTrace: {ex.StackTrace}");
            }
        }
    }
}
