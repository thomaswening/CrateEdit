using CrateEdit.Core.Results;

namespace CrateEdit.Core.Tags.Writing;

/// <summary>
/// The recovery material could not be stored in the local app data folder (e.g. the system drive
/// is full), so the write was not attempted: without it, a crash could not be recovered from
/// (<see href="https://github.com/thomaswening/CrateEdit/blob/main/docs/requirements.md#req-089">REQ-089</see>). The audio file is unchanged. The store's exception is kept in <see cref="Error.Cause"/>.
/// </summary>
public sealed record RecoveryUnavailable : Error
{
    public override string Code => ErrorCodes.RecoveryUnavailable;
}
