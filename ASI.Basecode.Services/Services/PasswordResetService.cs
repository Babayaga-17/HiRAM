using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Manager;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Services
{
    public class PasswordResetService : IPasswordResetService
    {
        private readonly IPasswordResetRepository _passwordResetRepository;
        private readonly IConfiguration _configuration;

        public PasswordResetService(IPasswordResetRepository passwordResetRepository, IConfiguration configuration)
        {
            _passwordResetRepository = passwordResetRepository;
            _configuration = configuration;
        }

        public async Task<bool> SendResetCodeAsync(string email)
        {
            var account = _passwordResetRepository.GetAccountByEmail(email?.Trim());
            if (account == null) return false;

            var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

            _passwordResetRepository.AddToken(new PasswordResetToken
            {
                AccountId = account.AccountId,
                TokenHash = Hash(code),
                ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                CreatedBy = "PasswordReset",
                CreatedAt = DateTime.UtcNow
            });

            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(_configuration["Smtp:From"]));
            message.To.Add(MailboxAddress.Parse(email.Trim()));
            message.Subject = "HiRAM password reset code";
            message.Body = new TextPart("plain")
            {
                Text = $"Your HiRAM verification code is {code}. It expires in 10 minutes."
            };

            using var client = new SmtpClient();
            await client.ConnectAsync(_configuration["Smtp:Host"], int.Parse(_configuration["Smtp:Port"]), SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(_configuration["Smtp:User"], _configuration["Smtp:Password"].Replace(" ", ""));
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            return true;
        }

        // Step 1: only checks the code. Does NOT use up the token yet.
        public int? VerifyCode(string email, string code)
        {
            var account = _passwordResetRepository.GetAccountByEmail(email?.Trim());
            if (account == null || string.IsNullOrWhiteSpace(code)) return null;

            var token = _passwordResetRepository.GetLatestActiveToken(account.AccountId);
            if (token == null || token.TokenHash != Hash(code.Trim())) return null;

            return token.TokenId;
        }

        // Step 2: saves the new password, then uses up the token.
        public bool SetNewPassword(string email, int tokenId, string newPassword)
        {
            var account = _passwordResetRepository.GetAccountByEmail(email?.Trim());
            if (account == null) return false;

            var token = _passwordResetRepository.GetLatestActiveToken(account.AccountId);
            if (token == null || token.TokenId != tokenId) return false;

            account.PasswordHash = PasswordManager.EncryptPassword(newPassword);
            account.FailedLoginAttempts = 0;
            account.LockoutEndAt = null;
            account.UpdatedBy = "PasswordReset";
            account.UpdatedAt = DateTime.UtcNow;

            token.UsedAt = DateTime.UtcNow;
            token.UpdatedBy = "PasswordReset";
            token.UpdatedAt = DateTime.UtcNow;

            _passwordResetRepository.Save();
            return true;
        }

        private static string Hash(string code)
        {
            using var sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(code)));
        }
    }
}
