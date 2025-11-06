using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.EntityFrameworkCore;
using ServerSignaling_Meeting.Dtos.Account;
using ServerSignaling_Meeting.Interfaces;
using ServerSignaling_Meeting.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SignalingServer.Controllers
{
    [Route("api/account")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly ITokenService _tokenService;
        private readonly SignInManager<AppUser> _signInManager;

        public AccountController(UserManager<AppUser> userManager, ITokenService tokenService, SignInManager<AppUser> signInManager)
        {
            _userManager = userManager;
            _tokenService = tokenService;
            _signInManager = signInManager;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto registerDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new
                    {
                        isSuccess = false,
                        errorMessage = "Invalid request data"
                    });
                }

                if (string.IsNullOrWhiteSpace(registerDto.Password))
                {
                    return BadRequest(new
                    {
                        isSuccess = false,
                        errorMessage = "Password is required!"
                    });
                }

                var user = new AppUser
                {
                    UserName = registerDto.Username,
                    Email = registerDto.Email,
                };

                var result = await _userManager.CreateAsync(user, registerDto.Password);

                if (!result.Succeeded)
                {
                    return BadRequest(new
                    {
                        isSuccess = false,
                        errorMessage = string.Join("; ", result.Errors.Select(e => e.Description))
                    });
                }

                var roleResult = await _userManager.AddToRoleAsync(user, "User");

                if (!roleResult.Succeeded)
                {
                    return BadRequest(new
                    {
                        isSuccess = false,
                        errorMessage = "User created but failed to assign role"
                    });
                }

                return Ok(new
                {
                    isSuccess = true,
                    userName = user.UserName,
                    email = user.Email,
                    token = _tokenService.CreateToken(user)
                });
            }
            catch (DbUpdateException ex)
            {
                return BadRequest(new
                {
                    isSuccess = false,
                    errorMessage = ex.InnerException?.Message ?? "Database error"
                });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    isSuccess = false,
                    errorMessage = "Invalid request data"
                });
            }

            var user = await _userManager.Users
                .FirstOrDefaultAsync(u => u.NormalizedUserName == loginDto.Username.ToUpper());

            if (user == null)
            {
                return Unauthorized(new
                {
                    isSuccess = false,
                    errorMessage = "Invalid username or password"
                });
            }

            var result = await _signInManager.CheckPasswordSignInAsync(user, loginDto.Password, true);

            if (result.IsLockedOut)
            {
                return Unauthorized(new
                {
                    isSuccess = false,
                    errorMessage = "User account is locked"
                });
            }

            if (!result.Succeeded)
            {
                return Unauthorized(new
                {
                    isSuccess = false,
                    errorMessage = "Invalid username or password"
                });
            }

            return Ok(new
            {
                isSuccess = true,
                userName = user.UserName,
                email = user.Email,
                token = _tokenService.CreateToken(user)
            });
        }


        [HttpPut("lock-user/{userId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> LockUser([FromRoute] Guid userId)
        {
            var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id.Equals(userId));
            if(user == null)
            {
                return Unauthorized("User is not exist");
            }

            await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));
            return Ok(new { message = "User has been locked", userId = user.Id });
        }

        [HttpPut("unlock-user/{userId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UnlockUser([FromRoute] Guid userId)
        {
            var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id.Equals(userId));
            if (user == null)
            {
                return Unauthorized("User is not exist");
            }

            await _userManager.SetLockoutEndDateAsync(user, null);
            return Ok(new { message = "User has been unlocked", userId = user.Id });
        }

    }
}
