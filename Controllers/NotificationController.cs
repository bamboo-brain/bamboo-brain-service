using BambooBrain_Service.Services.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.Azure.SignalR.Management;

namespace BambooBrain_Service.Controllers
{
    [ApiController]
    [Route("api/notifications")]
    [Authorize]
    public class NotificationController : ControllerBase
    {
        private readonly INotificationService _notifications;
        private readonly IConfiguration _config;

        public NotificationController(
            INotificationService notifications,
            IConfiguration config)
        {
            _notifications = notifications;
            _config = config;
        }

        // SignalR negotiate — frontend calls this to get connection info
        [HttpPost("negotiate")]
        public async Task<IActionResult> Negotiate()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var serviceManager = new ServiceManagerBuilder()
                .WithOptions(opt =>
                {
                    opt.ConnectionString = _config["SignalR:ConnectionString"]!;
                    opt.ServiceTransportType = ServiceTransportType.Transient;
                })
                .BuildServiceManager();

            var hubContext = await serviceManager.CreateHubContextAsync(
                "NotificationHub", default);

            var negotiateResponse = await hubContext.NegotiateAsync(
                new NegotiationOptions { UserId = userId });

            return Ok(new
            {
                url = negotiateResponse.Url,
                accessToken = negotiateResponse.AccessToken
            });
        }

        // Get notifications list
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var notifications = await _notifications.GetNotificationsAsync(userId);
            var unreadCount = await _notifications.GetUnreadCountAsync(userId);

            return Ok(new { notifications, unreadCount });
        }

        // Get unread count only (lightweight poll fallback)
        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var count = await _notifications.GetUnreadCountAsync(userId);
            return Ok(new { count });
        }

        // Mark single notification as read
        [HttpPatch("{id}/read")]
        public async Task<IActionResult> MarkRead(string id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            await _notifications.MarkAsReadAsync(id, userId);
            return Ok();
        }

        // Mark all as read
        [HttpPatch("read-all")]
        public async Task<IActionResult> MarkAllRead()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            await _notifications.MarkAllAsReadAsync(userId);
            return Ok();
        }

        // Delete notification
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            await _notifications.DeleteAsync(id, userId);
            return NoContent();
        }
    }
}
