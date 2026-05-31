namespace LocalLlmConsole.Services;

public sealed class ModelGatewayHostFactoryService
{
    private readonly Func<ModelGatewayRuntimeControllerActions, IModelGatewayRuntimeController> _createRuntimeController;
    private readonly Func<ModelGatewayOptions, IModelGatewayRuntimeController, IModelGatewayHost> _createGatewayHost;

    public ModelGatewayHostFactoryService(
        Func<ModelGatewayRuntimeControllerActions, IModelGatewayRuntimeController>? createRuntimeController = null,
        Func<ModelGatewayOptions, IModelGatewayRuntimeController, IModelGatewayHost>? createGatewayHost = null)
    {
        _createRuntimeController = createRuntimeController ?? DefaultRuntimeControllerFactory;
        _createGatewayHost = createGatewayHost ?? DefaultGatewayHostFactory;
    }

    public IModelGatewayRuntimeController CreateRuntimeController(ModelGatewayRuntimeControllerActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        return _createRuntimeController(actions);
    }

    public IModelGatewayHost CreateGatewayHost(
        ModelGatewayOptions options,
        IModelGatewayRuntimeController runtime)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(runtime);
        return _createGatewayHost(options, runtime);
    }

    private static ModelGatewayRuntimeController DefaultRuntimeControllerFactory(
        ModelGatewayRuntimeControllerActions actions)
        => new(actions);

    private static IModelGatewayHost DefaultGatewayHostFactory(
        ModelGatewayOptions options,
        IModelGatewayRuntimeController runtime)
        => new ModelGatewayService(options, runtime);
}
