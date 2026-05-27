using System.Collections.ObjectModel;

namespace LocalLlmConsole.ViewModels;

public sealed class WindowsPageViewModel
{
    public ObservableCollection<UiRow> Rows { get; } = new();

    public void ReplaceToolRows(WindowsToolSnapshot tools)
    {
        Rows.Clear();
        Rows.Add(new UiRow
        {
            C1 = "CPU tools",
            C2 = tools.CpuToolsInstalled ? "Ready" : "Incomplete",
            C3 = WindowsEnvironmentService.CpuDetails(tools)
        });
        Rows.Add(new UiRow
        {
            C1 = "CUDA tools",
            C2 = tools.CudaToolsInstalled ? "Ready" : "Incomplete",
            C3 = tools.CudaDetails,
            C4 = tools.NvidiaDriverVisible ? $"NVIDIA driver visible: {tools.NvidiaSmiPath}" : "NVIDIA driver not detected by nvidia-smi"
        });
        Rows.Add(new UiRow
        {
            C1 = "Vulkan tools",
            C2 = tools.VulkanToolsInstalled ? "Ready" : "Incomplete",
            C3 = tools.VulkanDetails
        });
        Rows.Add(new UiRow
        {
            C1 = "Intel oneAPI",
            C2 = tools.SyclToolsInstalled ? "Ready" : "Incomplete",
            C3 = tools.SyclDetails,
            C4 = tools.IntelGpuVisible ? "Intel GPU visible to sycl-ls" : "Intel GPU not detected by sycl-ls"
        });
    }
}
