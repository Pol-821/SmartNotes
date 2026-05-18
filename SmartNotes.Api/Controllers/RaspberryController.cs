using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartNotes.Api.Data;
using SmartNotes.Api.DTOs;
using SmartNotes.Api.Models;
using System.Security.Claims;
using SmartNotes.Api.Extensions;

namespace SmartNotes.Api.Controllers;

[ApiController]
[Route("api/raspberry")]
public class RaspberryController : ControllerBase
{
    private readonly SmartNotesDbContext _db;
    private readonly IConfiguration _config;

    public RaspberryController(SmartNotesDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    [HttpPost("register")]
    [Authorize]
    public IActionResult RegisterDevice([FromBody] RegisterRaspberryRequest request)
    {
        var userId = GetUserId();
        var device = _db.RaspberryDevices.FirstOrDefault(d => d.SerialNumber == request.SerialNumber);

        if (device == null) return NotFound("Aquest codi de vinculació no existeix.");
        if (device.UserId != null) return BadRequest("Aquest dispositiu ja està vinculat a un altre compte.");

        device.UserId = userId;
        _db.SaveChanges();

        return Ok(new { message = "Dispositiu vinculat correctament", serialNumber = device.SerialNumber });
    }

    [HttpGet("my-devices")]
    [Authorize]
    public IActionResult GetMyDevices()
    {
        var userId = GetUserId();
        var devices = _db.RaspberryDevices
            .Where(d => d.UserId == userId)
            .Select(d => new { d.SerialNumber, d.RegisteredAt })
            .ToList();

        return Ok(devices);
    }

    [HttpPost("unregister")]
    [Authorize]
    public IActionResult UnregisterDevice([FromBody] RegisterRaspberryRequest request)
    {
        var userId = GetUserId();
        var device = _db.RaspberryDevices.FirstOrDefault(d => d.SerialNumber == request.SerialNumber && d.UserId == userId);

        if (device == null) return NotFound("Dispositiu no trobat o no és teu.");

        device.UserId = null;
        _db.SaveChanges();

        return Ok("Dispositiu desvinculat");
    }

    [HttpPost("provision")]
    [Authorize(Roles = "Admin")]
    public IActionResult ProvisionNewDevice()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        var randomPart = new string(Enumerable.Repeat(chars, 4).Select(s => s[random.Next(s.Length)]).ToArray());
        var serial = $"RPI-{DateTime.UtcNow.Year}-{randomPart}";

        var newDevice = new RaspberryDevice
        {
            SerialNumber = serial,
            RegisteredAt = DateTime.UtcNow
        };

        _db.RaspberryDevices.Add(newDevice);
        _db.SaveChanges();

        return Ok(new { serialNumber = serial });
    }

    [HttpGet("check")]
    public IActionResult CheckDevice()
    {
        if (!Request.Headers.TryGetValue("X-Serial-Number", out var serial)) return Unauthorized("Falta el codi");
        if (!Request.Headers.TryGetValue("X-Raspberry-Key", out var apiKey)) return Unauthorized("Falta la clau API");

        var expectedKey = _config["Raspberry:ApiKey"];
        if (string.IsNullOrEmpty(expectedKey) || apiKey != expectedKey) return Unauthorized("Credencials invàlides");

        var device = _db.RaspberryDevices.FirstOrDefault(d => d.SerialNumber == serial);
        if (device == null) return Unauthorized("Dispositiu no registrat");

        return Ok(new { linked = device.UserId != null, userId = device.UserId });
    }

    private int GetUserId() => User.GetUserId();
}