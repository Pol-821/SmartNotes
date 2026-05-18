using SmartNotes.Api.DTOs;
using SmartNotes.Api.Models;

namespace SmartNotes.Api.Mappers;

public static class TranscriptionMapper
{
    public static TranscriptionJobDto ToDto(this TranscriptionJob job)
    {
        return new TranscriptionJobDto
        {
            Id = job.Id,
            Status = job.Status.ToString(),
            Result = job.Result,
            Summary = job.Summary,
            ErrorMessage = job.ErrorMessage
        };
    }
}