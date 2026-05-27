namespace LocalLlmConsole.Services;

public static class WslSetupCommands
{
    public const string RecommendedUbuntuDistro = "Ubuntu-24.04";
    public const string BuildToolsPackages = "git cmake build-essential pkg-config libcurl4-openssl-dev ccache ninja-build";
    public const string InstallBuildToolsCommand = "set -e; sudo apt update; sudo apt install -y " + BuildToolsPackages;
    public const string RemoveBuildToolsCommand = "set -e; sudo apt remove -y " + BuildToolsPackages + "; sudo apt autoremove -y";
    public const string VulkanToolsPackages = "libvulkan-dev glslc spirv-headers vulkan-tools mesa-vulkan-drivers";
    public const string InstallVulkanToolsCommand = "set -e; sudo apt update; sudo apt install -y " + VulkanToolsPackages + "; vulkaninfo --summary";
    public const string RemoveVulkanToolsCommand = "set -e; sudo apt remove -y " + VulkanToolsPackages + "; sudo apt autoremove -y";
    public const string SyclRuntimePackages = "libze1 libze-intel-gpu1 libze-dev intel-opencl-icd clinfo";
    public const string InstallSyclRuntimeCommand = "set -e; sudo apt update; sudo apt install -y " + SyclRuntimePackages + "; clinfo -l || true";
    public const string RemoveSyclRuntimeCommand = "set -e; sudo apt remove -y " + SyclRuntimePackages + "; sudo apt autoremove -y";
    public const string SyclOneApiPackages = "intel-oneapi-compiler-dpcpp-cpp intel-oneapi-mkl-devel intel-oneapi-dnnl-devel";
    public const string InstallSyclOneApiCommand =
        "set -e; " +
        "sudo apt-get update; sudo apt-get install -y wget ca-certificates gpg; " +
        "wget -qO- https://apt.repos.intel.com/intel-gpg-keys/GPG-PUB-KEY-INTEL-SW-PRODUCTS.PUB | gpg --dearmor | sudo tee /usr/share/keyrings/oneapi-archive-keyring.gpg >/dev/null; " +
        "echo 'deb [signed-by=/usr/share/keyrings/oneapi-archive-keyring.gpg] https://apt.repos.intel.com/oneapi all main' | sudo tee /etc/apt/sources.list.d/oneAPI.list; " +
        "sudo apt-get update; sudo apt-get install -y " + SyclOneApiPackages + "; " +
        "source /opt/intel/oneapi/setvars.sh --force >/dev/null 2>&1 || true; icpx --version | head -n 1; sycl-ls";
    public const string RemoveSyclOneApiCommand = "set -e; sudo apt remove -y " + SyclOneApiPackages + "; sudo apt autoremove -y";
    public const string ToolProbeCommand = """
set +e
missing_cpu=""
for tool in git cmake gcc g++ make pkg-config; do
  command -v "$tool" >/dev/null 2>&1 || missing_cpu="$missing_cpu $tool"
done
if command -v ninja >/dev/null 2>&1 || command -v ninja-build >/dev/null 2>&1; then :; else missing_cpu="$missing_cpu ninja"; fi
if [ -z "$missing_cpu" ]; then
  cmake_version=$(cmake --version 2>/dev/null | head -n 1 | sed 's/^cmake version //')
  echo "CPU=1"
  echo "CPU_SUMMARY=CPU OK${cmake_version:+, CMake $cmake_version}"
else
  echo "CPU=0"
  echo "CPU_SUMMARY=CPU missing:${missing_cpu}"
fi

cuda_nvcc=
if command -v nvcc >/dev/null 2>&1; then
  cuda_nvcc=$(command -v nvcc)
elif [ -x /usr/local/cuda/bin/nvcc ]; then
  cuda_nvcc=/usr/local/cuda/bin/nvcc
else
  for candidate in /usr/local/cuda*/bin/nvcc; do
    if [ -x "$candidate" ]; then cuda_nvcc="$candidate"; break; fi
  done
fi
cuda_lib=$(find /usr/local/cuda* /usr/lib /usr/lib/x86_64-linux-gnu -maxdepth 5 \( -name 'libcudart.so' -o -name 'libcudart.so.*' \) 2>/dev/null | head -n 1 || true)
if [ -z "$cuda_lib" ] && command -v ldconfig >/dev/null 2>&1; then
  cuda_lib=$(ldconfig -p | awk '/libcudart\.so/{print $NF; exit}')
fi
if [ -n "$cuda_nvcc" ] && [ -n "$cuda_lib" ]; then
  cuda_version=$("$cuda_nvcc" --version 2>/dev/null | awk -F'release ' '/release/{split($2,a,","); print a[1]; exit}')
  echo "CUDA=1"
  echo "CUDA_SUMMARY=CUDA OK${cuda_version:+ $cuda_version}"
else
  echo "CUDA=0"
  if [ -z "$cuda_nvcc" ]; then echo "CUDA_SUMMARY=CUDA missing nvcc"; else echo "CUDA_SUMMARY=CUDA missing libcudart"; fi
fi

missing_vulkan=""
command -v glslc >/dev/null 2>&1 || missing_vulkan="$missing_vulkan glslc"
command -v vulkaninfo >/dev/null 2>&1 || missing_vulkan="$missing_vulkan vulkaninfo"
if [ -f /usr/include/vulkan/vulkan.h ]; then :; else missing_vulkan="$missing_vulkan libvulkan-dev"; fi
if [ -d /usr/include/spirv ] || [ -d /usr/include/SPIRV ]; then :; else missing_vulkan="$missing_vulkan spirv-headers"; fi
vulkan_lib=$(ldconfig -p 2>/dev/null | awk '/libvulkan\.so/{print $NF; exit}')
if [ -z "$vulkan_lib" ] && [ -f /usr/lib/x86_64-linux-gnu/libvulkan.so ]; then vulkan_lib=/usr/lib/x86_64-linux-gnu/libvulkan.so; fi
if [ -z "$vulkan_lib" ]; then missing_vulkan="$missing_vulkan libvulkan.so"; fi
if [ -z "$missing_vulkan" ]; then
  vulkan_summary=$(vulkaninfo --summary 2>/dev/null)
  if [ "$?" -eq 0 ]; then
    vulkan_device=$(printf '%s\n' "$vulkan_summary" | awk -F'= ' '/deviceName/{print $2; exit}')
    echo "VULKAN=1"
    echo "VULKAN_SUMMARY=Vulkan OK${vulkan_device:+, $vulkan_device}"
  else
    echo "VULKAN=0"
    echo "VULKAN_SUMMARY=Vulkan missing usable driver/device"
  fi
else
  echo "VULKAN=0"
  echo "VULKAN_SUMMARY=Vulkan missing:${missing_vulkan}"
fi

sycl_icpx=
if command -v icpx >/dev/null 2>&1; then
  sycl_icpx=$(command -v icpx)
elif [ -f /opt/intel/oneapi/setvars.sh ]; then
  source /opt/intel/oneapi/setvars.sh --force >/dev/null 2>&1 || true
  command -v icpx >/dev/null 2>&1 && sycl_icpx=$(command -v icpx)
fi
if [ -z "$sycl_icpx" ] && [ -x /opt/intel/oneapi/compiler/latest/bin/icpx ]; then
  sycl_icpx=/opt/intel/oneapi/compiler/latest/bin/icpx
fi
if [ -n "$sycl_icpx" ]; then
  sycl_out=$(sycl-ls 2>/dev/null || true)
  sycl_device=$(printf '%s\n' "$sycl_out" | grep -i 'level_zero.*gpu' | head -n 1)
  if [ -n "$sycl_device" ]; then
    sycl_ver=$("$sycl_icpx" --version 2>/dev/null | head -n 1)
    echo "SYCL=1"
    echo "SYCL_SUMMARY=SYCL OK${sycl_ver:+, $sycl_ver}${sycl_device:+, $sycl_device}"
  else
    echo "SYCL=0"
    echo "SYCL_SUMMARY=SYCL missing Level Zero GPU device"
  fi
else
  echo "SYCL=0"
  echo "SYCL_SUMMARY=SYCL missing icpx (oneAPI not installed)"
fi
""";
    public const string CudaToolkitPreflightCommand = """
set -e
cuda_nvcc=; if command -v nvcc >/dev/null 2>&1; then cuda_nvcc=$(command -v nvcc); elif [ -x /usr/local/cuda/bin/nvcc ]; then cuda_nvcc=/usr/local/cuda/bin/nvcc; else for candidate in /usr/local/cuda*/bin/nvcc; do if [ -x "$candidate" ]; then cuda_nvcc="$candidate"; break; fi; done; fi
if [ -n "$cuda_nvcc" ]; then "$cuda_nvcc" --version | head -n 4; fi
cuda_lib=$(find /usr/local/cuda* /usr/lib /usr/lib/x86_64-linux-gnu -maxdepth 5 \( -name 'libcudart.so' -o -name 'libcudart.so.*' \) 2>/dev/null | head -n 1 || true)
if [ -z "$cuda_lib" ] && command -v ldconfig >/dev/null 2>&1; then cuda_lib=$(ldconfig -p | awk '/libcudart\.so/{print $NF; exit}'); fi
if [ -n "$cuda_nvcc" ] && [ -n "$cuda_lib" ]; then exit 0; fi
if command -v nvidia-smi >/dev/null 2>&1; then nvidia-smi -L 2>/dev/null || true; fi
if [ -z "$cuda_nvcc" ]; then echo "CUDA compiler nvcc was not found inside this WSL distro." >&2; fi
if [ -z "$cuda_lib" ]; then echo "CUDA runtime library libcudart was not found inside this WSL distro." >&2; fi
echo "CPU build tools do not include CUDA. Use WSL Linux > Install CUDA, or install the NVIDIA CUDA Toolkit/runtime development packages in Ubuntu/WSL manually, then retry." >&2
exit 2
""";
    public const string VulkanToolsPreflightCommand = """
set -e
missing=""
command -v glslc >/dev/null 2>&1 || missing="$missing glslc"
command -v vulkaninfo >/dev/null 2>&1 || missing="$missing vulkaninfo"
if [ -f /usr/include/vulkan/vulkan.h ]; then :; else missing="$missing libvulkan-dev"; fi
if [ -d /usr/include/spirv ] || [ -d /usr/include/SPIRV ]; then :; else missing="$missing spirv-headers"; fi
vulkan_lib=$(ldconfig -p 2>/dev/null | awk '/libvulkan\.so/{print $NF; exit}')
if [ -z "$vulkan_lib" ] && [ -f /usr/lib/x86_64-linux-gnu/libvulkan.so ]; then vulkan_lib=/usr/lib/x86_64-linux-gnu/libvulkan.so; fi
if [ -z "$vulkan_lib" ]; then missing="$missing libvulkan.so"; fi
if [ -n "$missing" ]; then
  echo "Vulkan build dependencies were not complete inside this WSL distro: $missing" >&2
  echo "Use WSL Linux > Install Vulkan, or install the Ubuntu Vulkan packages manually: libvulkan-dev glslc spirv-headers vulkan-tools." >&2
  exit 2
fi
if ! vulkaninfo --summary; then
  echo "Vulkan tools are installed, but vulkaninfo could not see a usable Vulkan driver/device inside this WSL distro." >&2
  echo "Install or update the Windows GPU driver with WSL Vulkan support, then retry." >&2
  exit 2
fi
""";
    public const string SyclToolsPreflightCommand = """
set -e
if [ -f /opt/intel/oneapi/setvars.sh ]; then
  source /opt/intel/oneapi/setvars.sh --force >/dev/null 2>&1 || true
fi
if ! command -v icx >/dev/null 2>&1; then
  echo "Intel oneAPI C compiler icx was not found." >&2
  exit 2
fi
if ! command -v icpx >/dev/null 2>&1; then
  echo "Intel oneAPI DPC++ compiler icpx was not found." >&2
  exit 2
fi
if ! command -v sycl-ls >/dev/null 2>&1; then
  echo "sycl-ls was not found after sourcing oneAPI environment." >&2
  exit 2
fi
icpx --version | head -n 1
if ! sycl-ls 2>/dev/null | grep -qi 'level_zero.*gpu'; then
  echo "No Level Zero Intel GPU device is visible to sycl-ls. Install Intel GPU runtime packages and make sure WSL can access the GPU." >&2
  exit 2
fi
sycl-ls
""";
    public const string CudaToolkitPackage = "cuda-toolkit-13-2";
    public const string CudaRemovePackages = "cuda-toolkit-13-2 cuda-compiler-13-2 cuda-cudart-13-2 cuda-cudart-dev-13-2 cuda-keyring";
    public const string CudaKeyringUrl = "https://developer.download.nvidia.com/compute/cuda/repos/wsl-ubuntu/x86_64/cuda-keyring_1.1-1_all.deb";
    public const string InstallCudaToolkitCommand =
        "set -e; tmp=$(mktemp -d); trap 'rm -rf \"$tmp\"' EXIT; cd \"$tmp\"; " +
        "sudo apt-get update; sudo apt-get install -y wget ca-certificates; " +
        "wget -q " + CudaKeyringUrl + "; " +
        "sudo dpkg -i cuda-keyring_1.1-1_all.deb; " +
        "sudo apt-get update; sudo apt-get install -y " + CudaToolkitPackage + "; " +
        "cuda_nvcc=; if command -v nvcc >/dev/null 2>&1; then cuda_nvcc=$(command -v nvcc); elif [ -x /usr/local/cuda/bin/nvcc ]; then cuda_nvcc=/usr/local/cuda/bin/nvcc; else for candidate in /usr/local/cuda*/bin/nvcc; do if [ -x \"$candidate\" ]; then cuda_nvcc=\"$candidate\"; break; fi; done; fi; " +
        "if [ -n \"$cuda_nvcc\" ]; then \"$cuda_nvcc\" --version | head -n 4; else echo 'nvcc still not found after CUDA install' >&2; exit 2; fi; " +
        "if ldconfig -p 2>/dev/null | grep -q 'libcudart\\.so' || find /usr/local/cuda* /usr/lib /usr/lib/x86_64-linux-gnu -maxdepth 5 \\( -name 'libcudart.so' -o -name 'libcudart.so.*' \\) 2>/dev/null | grep -q .; then echo 'CUDA Toolkit ready for llama.cpp builds.'; else echo 'libcudart still not found after CUDA install' >&2; exit 2; fi";
    public const string RemoveCudaToolkitCommand = "set -e; sudo apt remove -y " + CudaRemovePackages + "; sudo apt autoremove -y";

    public static string InstallUbuntuAndBuildToolsPowerShell(string wslExe, string distro = RecommendedUbuntuDistro)
    {
        var wsl = CommandLineService.PowerShellQuote(wslExe);
        var distroQ = CommandLineService.PowerShellQuote(distro);
        return string.Join("; ", new[]
        {
            $"& {wsl} --install -d {distroQ}",
            "if ($LASTEXITCODE -ne 0) { Write-Host 'Ubuntu install did not complete. If Windows asks for a reboot, reboot and then use Install CPU Tools from the app.'; return }",
            "Write-Host ''",
            "Write-Host 'Installing llama.cpp CPU build tools inside Ubuntu...'",
            CommandLineService.PowerShellWslBashScriptCommand(wslExe, distro, InstallBuildToolsCommand),
            "if ($LASTEXITCODE -ne 0) { Write-Host 'Build tool install did not complete. Finish Ubuntu first-run setup or reboot if needed, then use Install CPU Tools from the app.' }"
        });
    }

    public static string DeleteWslPowerShell(string wslExe)
    {
        var wsl = CommandLineService.PowerShellQuote(wslExe);
        return string.Join("; ", new[]
        {
            "$answer = Read-Host 'Type DELETE WSL to uninstall the WSL package'",
            "if ($answer -ne 'DELETE WSL') { Write-Host 'Cancelled.'; return }",
            $"& {wsl} --shutdown",
            $"& {wsl} --uninstall"
        });
    }

    public static string DeleteUbuntuPowerShell(string wslExe, string distro)
    {
        var wsl = CommandLineService.PowerShellQuote(wslExe);
        var distroQ = CommandLineService.PowerShellQuote(distro);
        var expected = CommandLineService.PowerShellQuote(distro);
        var promptDistro = distro.Replace("'", "''");
        return string.Join("; ", new[]
        {
            $"$answer = Read-Host 'Type {promptDistro} to unregister and delete this distro'",
            $"if ($answer -ne {expected}) {{ Write-Host 'Cancelled.'; return }}",
            $"& {wsl} --terminate {distroQ}",
            $"& {wsl} --unregister {distroQ}"
        });
    }
}
