using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AWSS3Zip.Entity.Models;

/// <summary>
/// Entity - flattens a LogEvent request record with its parent IIS log record
/// </summary>
/// <param name="id">LogEvent ID</param>
public class IISLogEvent(string id)
{
    public readonly string Id = id;

    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int RowId { get; set; }

    public string? MessageType { get; set; }
    public string? Owner { get; set; }
    public string? LogGroup { get; set; }
    public string? LogStream { get; set; }
    public string? SubscriptionFilters { get; set; }
    public DateTime? DateTime { get; set; }
    public string? RequestMessage { get; set; }
}
