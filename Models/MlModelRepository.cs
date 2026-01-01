namespace grefurBackend.Models.AlarmConfiguration;

public interface MlModelRepository
{
    MlModelMetadata? getActiveModel(
        string customerId,
        string targetMeasurementId);

    void saveModel(
        MlModelMetadata metadata,
        Stream modelStream);
}
