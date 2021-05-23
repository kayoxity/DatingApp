using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using API.Data;
using API.DTO;
using API.Entities;
using API.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Controllers
{
    public class AccountController : BaseApiController
    {
        private readonly DataContext _context;
        private readonly ITokenService _tokenService;
        public AccountController(DataContext context, ITokenService tokenService)
        {
            _tokenService = tokenService;
            _context = context;
        }

        [HttpPost("register")]
        public async Task<ActionResult<UserDTO>> Register(RegisterDTO registerDTO)
        {
            if (await UserExists(registerDTO.Username))
                return BadRequest("Username is taken");

            using var hmac = new HMACSHA512();

            var user = new AppUser()
            {
                UserName = registerDTO.Username.ToLower(),
                PasswordHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(registerDTO.Password)),
                PasswordSalt = hmac.Key
            };

            await _context.Users.AddAsync(user);

            await _context.SaveChangesAsync();

            return new UserDTO()
            {
                Username = user.UserName,
                Token = _tokenService.CreateToken(user)
            };
        }

        [HttpPost("login")]
        public async Task<ActionResult<UserDTO>> Login(LoginDTO loginDTO)
        {
            AppUser user = await _context.Users.SingleOrDefaultAsync(x => x.UserName == loginDTO.Username.ToLower());

            if (user == null)
                return Unauthorized("Username not present");

            using var hmac = new HMACSHA512(user.PasswordSalt);

            var PasswordHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(loginDTO.Password));

            if (PasswordHash.Length != user.PasswordHash.Length)
                return Unauthorized("Wrong Password");

            for (int i = 0; i < PasswordHash.Length; i++)
            {
                if (PasswordHash[i] != user.PasswordHash[i])
                    return Unauthorized("Wrong Password");
            }

            return new UserDTO()
            {
                Username = user.UserName,
                Token = _tokenService.CreateToken(user)
            };
        }

        private async Task<bool> UserExists(string username)
        {
            return await _context.Users.AnyAsync(x => x.UserName == username.ToLower());
        }
    }
}