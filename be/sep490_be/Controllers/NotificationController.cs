using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using sep490_be.Models;
using sep490_be.DTO;
using sep490_be.Enums;

namespace sep490_be.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class NotificationController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly UserManager<IdentityUser> _userManager;

        public NotificationController(ApplicationDbContext dbContext, UserManager<IdentityUser> userManager)
        {
            _dbContext = dbContext;
            _userManager = userManager;
        }

        // POST: api/Notification/register-device
        [HttpPost("register-device")]
        public async Task<IActionResult> RegisterDevice([FromBody] DTO.Notification.RegisterDeviceTokenDto dto)
        {
            try
            {
                var username = User.Identity?.Name;
                if (string.IsNullOrEmpty(username))
                {
                    return Unauthorized();
                }

                var user = await _userManager.FindByNameAsync(username);
                if (user == null)
                {
                    return NotFound(new { success = false, message = "User not found" });
                }

                var existingToken = await _dbContext.UserDeviceTokens
                    .FirstOrDefaultAsync(t => t.FcmToken == dto.FcmToken);

                if (existingToken != null)
                {
                    existingToken.UserId = user.Id;
                    existingToken.LastActiveAt = DateTime.UtcNow;
                    existingToken.DeviceType = dto.DeviceType;
                }
                else
                {
                    var deviceToken = new UserDeviceToken
                    {
                        UserId = user.Id,
                        FcmToken = dto.FcmToken,
                        DeviceType = dto.DeviceType,
                        LastActiveAt = DateTime.UtcNow
                    };
                    _dbContext.UserDeviceTokens.Add(deviceToken);
                }

                await _dbContext.SaveChangesAsync();
                return Ok(new { success = true, message = "Device token registered successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, message = ex.Message });
            }
        }

        // POST: api/Notification/test-fcm
        [HttpPost("test-fcm")]
        public async Task<IActionResult> TestFcm([FromBody] DTO.Notification.RegisterDeviceTokenDto? dto)
        {
            try
            {
                var username = User.Identity?.Name;
                var user = !string.IsNullOrEmpty(username) ? await _userManager.FindByNameAsync(username) : null;
                
                var tokens = new List<string>();
                if (!string.IsNullOrEmpty(dto?.FcmToken) && dto.FcmToken != "string")
                {
                    tokens.Add(dto.FcmToken);
                }
                else if (user != null)
                {
                    tokens = await _dbContext.UserDeviceTokens
                        .Where(t => t.UserId == user.Id)
                        .Select(t => t.FcmToken)
                        .ToListAsync();
                }

                if (!tokens.Any())
                {
                    return BadRequest(new { success = false, message = "Không tìm thấy FCM Token nào của user này trong Database. Hãy đăng nhập lại trên Mobile để nạp token!" });
                }

                var messaging = FirebaseAdmin.Messaging.FirebaseMessaging.DefaultInstance;
                if (messaging == null)
                {
                    return StatusCode(500, new { success = false, message = "Firebase Admin SDK chưa được khởi tạo. Kiểm tra lại file firebase_service_account.json trên server." });
                }

                var results = new List<object>();
                foreach (var token in tokens)
                {
                    try
                    {
                        var msg = new FirebaseAdmin.Messaging.Message()
                        {
                            Token = token,
                            Notification = new FirebaseAdmin.Messaging.Notification()
                            {
                                Title = "Test Push Notification",
                                Body = "Hệ thống IELTSmart gửi thông báo thành công!"
                            },
                            Android = new FirebaseAdmin.Messaging.AndroidConfig()
                            {
                                Priority = FirebaseAdmin.Messaging.Priority.High,
                                Notification = new FirebaseAdmin.Messaging.AndroidNotification()
                                {
                                    ChannelId = "ieltsmart_channel",
                                    Sound = "default",
                                    Icon = "ic_launcher",
                                    ClickAction = "FLUTTER_NOTIFICATION_CLICK"
                                }
                            }
                        };
                        var resp = await messaging.SendAsync(msg);
                        results.Add(new { token = token, status = "Success", messageId = resp });
                    }
                    catch (Exception ex)
                    {
                        results.Add(new { token = token, status = "Failed", error = ex.Message });
                    }
                }

                return Ok(new { success = true, results = results });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // GET: api/Notification
        [HttpGet]
        public async Task<IActionResult> GetMyNotifications()
        {
            try
            {
                var username = User.Identity?.Name;
                if (string.IsNullOrEmpty(username))
                {
                    return Unauthorized();
                }

                var user = await _userManager.FindByNameAsync(username);
                if (user == null)
                {
                    return NotFound(new { success = false, message = "User not found" });
                }

                var roles = await _userManager.GetRolesAsync(user);
                var notificationsQuery = _dbContext.Notifications.AsQueryable();

                bool isAdmin = roles.Any(r => 
                    string.Equals(r, "Admin", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(r, "Quản lý trung tâm", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(r, "Ban vận hành", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(r, "Ban chuyên môn", StringComparison.OrdinalIgnoreCase)
                );

                bool isStudent = roles.Any(r => 
                    string.Equals(r, "Student", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(r, "Học sinh", StringComparison.OrdinalIgnoreCase)
                );

                bool isTeacher = roles.Any(r => 
                    string.Equals(r, "Teacher", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(r, "Giáo viên", StringComparison.OrdinalIgnoreCase)
                );

                if (isAdmin)
                {
                    // No class filter needed for admins
                }
                else if (isStudent)
                {
                    var student = await _dbContext.Students.FirstOrDefaultAsync(s => s.Email != null && user.Email != null && s.Email.ToLower() == user.Email.ToLower());
                    if (student == null)
                    {
                        return Ok(new { success = true, data = new List<Notification>() });
                    }

                    var classIds = await _dbContext.StudentClasses
                        .Where(sc => sc.StudentId == student.Id)
                        .Select(sc => sc.ClassId)
                        .ToListAsync();

                    notificationsQuery = notificationsQuery.Where(n => 
                        (n.ClassId.HasValue && classIds.Contains(n.ClassId.Value)) || 
                        (n.TargetType == (int)NotificationTargetType.User && n.TargetId == student.Id)
                    );
                }
                else if (isTeacher)
                {
                    var teacher = await _dbContext.Teachers.FirstOrDefaultAsync(t => t.Email != null && user.Email != null && t.Email.ToLower() == user.Email.ToLower());
                    if (teacher == null)
                    {
                        return Ok(new { success = true, data = new List<Notification>() });
                    }

                    var classIds = await _dbContext.Classes
                        .Where(c => c.TeacherId == teacher.Id)
                        .Select(c => c.Id)
                        .ToListAsync();

                    notificationsQuery = notificationsQuery.Where(n => 
                        n.ClassId.HasValue && classIds.Contains(n.ClassId.Value)
                    );
                }
                else
                {
                    // Parents / Others do not receive class notifications
                    return Ok(new { success = true, data = new List<Notification>() });
                }

                var notifications = await notificationsQuery
                    .OrderByDescending(n => n.SentAt)
                    .Take(50)
                    .ToListAsync();

                return Ok(new { success = true, data = notifications });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, message = ex.Message });
            }
        }

        // POST: api/Notification/mark-all-read
        [HttpPost("mark-all-read")]
        public async Task<IActionResult> MarkAllAsRead()
        {
            try
            {
                var username = User.Identity?.Name;
                if (string.IsNullOrEmpty(username))
                {
                    return Unauthorized();
                }

                var user = await _userManager.FindByNameAsync(username);
                if (user == null)
                {
                    return NotFound(new { success = false, message = "User not found" });
                }

                var roles = await _userManager.GetRolesAsync(user);
                List<Notification> notificationsToUpdate;

                bool isAdmin = roles.Any(r => 
                    string.Equals(r, "Admin", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(r, "Quản lý trung tâm", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(r, "Ban vận hành", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(r, "Ban chuyên môn", StringComparison.OrdinalIgnoreCase)
                );

                bool isStudent = roles.Any(r => 
                    string.Equals(r, "Student", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(r, "Học sinh", StringComparison.OrdinalIgnoreCase)
                );

                bool isTeacher = roles.Any(r => 
                    string.Equals(r, "Teacher", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(r, "Giáo viên", StringComparison.OrdinalIgnoreCase)
                );

                if (isAdmin)
                {
                    notificationsToUpdate = await _dbContext.Notifications
                        .Where(n => n.Status == (int)NotificationStatus.Unread)
                        .ToListAsync();
                }
                else if (isStudent)
                {
                    var student = await _dbContext.Students.FirstOrDefaultAsync(s => s.Email != null && user.Email != null && s.Email.ToLower() == user.Email.ToLower());
                    if (student == null) return Ok(new { success = true });

                    var classIds = await _dbContext.StudentClasses
                        .Where(sc => sc.StudentId == student.Id)
                        .Select(sc => sc.ClassId)
                        .ToListAsync();

                    notificationsToUpdate = await _dbContext.Notifications
                        .Where(n => n.Status == (int)NotificationStatus.Unread && 
                                    ((n.ClassId.HasValue && classIds.Contains(n.ClassId.Value)) || 
                                     (n.TargetType == (int)NotificationTargetType.User && n.TargetId == student.Id)))
                        .ToListAsync();
                }
                else if (isTeacher)
                {
                    var teacher = await _dbContext.Teachers.FirstOrDefaultAsync(t => t.Email != null && user.Email != null && t.Email.ToLower() == user.Email.ToLower());
                    if (teacher == null) return Ok(new { success = true });

                    var classIds = await _dbContext.Classes
                        .Where(c => c.TeacherId == teacher.Id)
                        .Select(c => c.Id)
                        .ToListAsync();

                    notificationsToUpdate = await _dbContext.Notifications
                        .Where(n => n.Status == (int)NotificationStatus.Unread && 
                                    n.ClassId.HasValue && classIds.Contains(n.ClassId.Value))
                        .ToListAsync();
                }
                else
                {
                    return Ok(new { success = true });
                }

                foreach (var notif in notificationsToUpdate)
                {
                    notif.Status = (int)NotificationStatus.Read;
                }

                await _dbContext.SaveChangesAsync();
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, message = ex.Message });
            }
        }

        // POST: api/Notification/{id}/read
        [HttpPost("{id}/read")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            try
            {
                var notification = await _dbContext.Notifications.FindAsync(id);
                if (notification == null)
                {
                    return NotFound(new { success = false, message = "Notification not found" });
                }

                notification.Status = (int)NotificationStatus.Read;
                await _dbContext.SaveChangesAsync();

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, message = ex.Message });
            }
        }
    }
}
