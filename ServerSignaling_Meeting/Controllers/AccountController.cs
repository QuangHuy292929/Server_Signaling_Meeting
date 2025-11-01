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
                    return BadRequest(ModelState);
                }

                var user = new AppUser
                {
                    UserName = registerDto.Username,
                    Email = registerDto.Email,
                };

                if (string.IsNullOrWhiteSpace(registerDto.Password))
                {
                    ModelState.AddModelError("Password", "Password is required!");
                    return BadRequest(ModelState);
                }

                var result = await _userManager.CreateAsync(user, registerDto.Password);
                if (result.Succeeded)
                {
                    var roleResult = await _userManager.AddToRoleAsync(user, "User");
                    if (roleResult.Succeeded)
                    {
                        return Ok(
                        new NewAccountDto
                        {
                            UserName = user.UserName,
                            Email = user.Email,
                            Token = _tokenService.CreateToken(user)
                        });
                    }
                    else
                    {
                        return BadRequest(roleResult.Errors);
                    }

                }
                else
                {
                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError(error.Code, error.Description);
                    }
                    return BadRequest(ModelState);
                }
            }
            catch (DbUpdateException ex)
            {
                return BadRequest(new
                {
                    message = "Lỗi khi lưu dữ liệu",
                    details = ex.InnerException?.Message
                });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = await _userManager.Users.FirstOrDefaultAsync(u => u.UserName.Equals(loginDto.Username));
            if (user == null)
            {
                return Unauthorized("Invalid username or password.");
            }

            var result = await _signInManager.CheckPasswordSignInAsync(user, loginDto.Password, true);
            if(result.IsLockedOut)
            {
                return Unauthorized("User account is locked.");
            }
            if(!result.Succeeded) return Unauthorized("Invalid username or password.");
            return Ok(
                new NewAccountDto
                {
                    UserName = user.UserName,
                    Email = user.Email,
                    Token = _tokenService.CreateToken(user)
                }
            );
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
