using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FaceStone.Web.Models;

/// <summary>
/// Project Code entity for Transportation and Logistics operations
/// Represents registered construction / engineering project sites
/// </summary>
public class ProjectCode
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Unique Project Code (e.g. PRJ-NAC-R3, PRJ-MONO-E2, PRJ-DABAA-NUC)
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Official Project Name in Arabic (e.g. مشروع العاصمة الإدارية - الحي السكني R3)
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>
    /// Client or contractor name executing the project (e.g. شركة المقاولون العرب)
    /// </summary>
    [MaxLength(200)]
    public string? ClientName { get; set; }

    /// <summary>
    /// Optional link to existing StorageClientDestination
    /// </summary>
    public int? StorageClientDestinationId { get; set; }
    public StorageClientDestination? StorageClientDestination { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

