using Microsoft.EntityFrameworkCore;
using ProflowApp.Data;
using ProflowApp.Models;

namespace ProFlowApp.Services
{
    public class AuditService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuditService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task LogAsync(
            string action,
            string entity,
            string? entityId = null,
            string? detail = null,
            string? overrideUsername = null,
            string? overrideUserID = null)
        {
            var httpContext = _httpContextAccessor.HttpContext;

            var userID = overrideUserID ?? httpContext?.Session.GetString("UserID");
            var username = overrideUsername ?? httpContext?.Session.GetString("Username");
            var role = httpContext?.Session.GetString("Role");

            var ipAddress = httpContext?.Request.Headers["X-Forwarded-For"]
                                .FirstOrDefault()
                                ?? httpContext?.Connection.RemoteIpAddress?.ToString()
                                ?? "Unknown";

            var log = new AuditLog
            {
                UserID = userID,
                Username = username,
                Role = role,
                Action = action,
                Entity = entity,
                EntityId = entityId,
                Detail = detail,
                Timestamp = DateTime.Now,
                IpAddress = ipAddress
            };

            _context.AuditLogs.Add(log);
            await _context.SaveChangesAsync();
        }
    }
}