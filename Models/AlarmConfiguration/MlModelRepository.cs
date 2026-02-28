using System.IO;

namespace grefurBackend.Models.AlarmConfiguration;

public interface MlModelRepository
{
    MlModelMetadata? GetActiveModel(
        string customerId,
        string targetMeasurementId);

    void SaveModel(
        MlModelMetadata metadata,
        Stream modelStream);
}