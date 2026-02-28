namespace grefurBackend.Events.Device;

public record DeviceRegistrationRequest(
	string DeviceId,
	string CustomerId,
	string DeviceType,
	string SoftwareVersion,
	string HardwareVersion,
	bool IsNested,
	string? DeviceName = null,
	string? ServiceValidationToken = null
);
