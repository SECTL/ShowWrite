#include <windows.h>
#include <mfapi.h>
#include <mfidl.h>
#include <mfreadwrite.h>
#include <mferror.h>
#include <iostream>
#include <string>
#include <atomic>
#include <chrono>
#include <thread>
#include <vector>

#pragma comment(lib, "mfplat.lib")
#pragma comment(lib, "mf.lib")
#pragma comment(lib, "mfuuid.lib")
#pragma comment(lib, "mfreadwrite.lib")
#pragma comment(lib, "shlwapi.lib")

constexpr const wchar_t* SHARED_MEMORY_NAME = L"ShowWriteCameraFrame";
constexpr const wchar_t* PIPE_NAME = L"\\\\.\\pipe\\ShowWriteCameraPipe";
constexpr DWORD SHARED_MEMORY_SIZE = 1920 * 1080 * 4 + 1024;
constexpr DWORD HEADER_SIZE = 32;

template <class T> void SafeRelease(T** ppT) {
    if (*ppT) {
        (*ppT)->Release();
        *ppT = nullptr;
    }
}

class CameraService {
private:
    IMFSourceReader* pReader = nullptr;
    IMFMediaSource* pSource = nullptr;
    HANDLE hMapFile = nullptr;
    LPVOID pSharedMemory = nullptr;
    HANDLE hPipe = nullptr;
    std::atomic<bool> isRunning{false};
    std::atomic<long long> frameCount{0};
    int cameraIndex = 0;
    int width = 1920;
    int height = 1080;
    int fps = 60;
    DWORD videoFormat = 0;

public:
    ~CameraService() {
        Stop();
    }

    bool Start(int cameraIndex, int width, int height, int fps) {
        this->cameraIndex = cameraIndex;
        this->width = width;
        this->height = height;
        this->fps = fps;

        isRunning = true;

        std::cout << "[CameraService] Starting camera service..." << std::endl;
        std::cout << "[CameraService] Camera index: " << cameraIndex
            << ", Resolution: " << width << "x" << height << std::endl;

        HRESULT hr = CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);
        if (FAILED(hr)) {
            std::cerr << "[CameraService] CoInitializeEx failed: 0x" << std::hex << hr << std::endl;
            return false;
        }

        hr = MFStartup(MF_VERSION);
        if (FAILED(hr)) {
            std::cerr << "[CameraService] MFStartup failed: 0x" << std::hex << hr << std::endl;
            CoUninitialize();
            return false;
        }

        if (!InitializeSharedMemory()) {
            std::cerr << "[CameraService] Failed to initialize shared memory" << std::endl;
            Cleanup();
            return false;
        }

        if (!InitializePipeServer()) {
            std::cerr << "[CameraService] Failed to initialize pipe server" << std::endl;
            Cleanup();
            return false;
        }

        if (!InitializeCamera()) {
            std::cerr << "[CameraService] Failed to initialize camera" << std::endl;
            Cleanup();
            return false;
        }

        RunCameraService();
        return true;
    }

    void Stop() {
        std::cout << "[CameraService] Stopping camera service..." << std::endl;
        isRunning = false;
        Cleanup();
    }

private:
    bool InitializeSharedMemory() {
        std::cout << "[CameraService] Initializing shared memory..." << std::endl;

        hMapFile = CreateFileMappingW(
            INVALID_HANDLE_VALUE,
            nullptr,
            PAGE_READWRITE,
            0,
            SHARED_MEMORY_SIZE,
            SHARED_MEMORY_NAME
        );

        if (hMapFile == nullptr) {
            std::cerr << "[CameraService] CreateFileMapping failed: " << GetLastError() << std::endl;
            return false;
        }

        pSharedMemory = MapViewOfFile(
            hMapFile,
            FILE_MAP_ALL_ACCESS,
            0,
            0,
            SHARED_MEMORY_SIZE
        );

        if (pSharedMemory == nullptr) {
            std::cerr << "[CameraService] MapViewOfFile failed: " << GetLastError() << std::endl;
            CloseHandle(hMapFile);
            hMapFile = nullptr;
            return false;
        }

        std::cout << "[CameraService] Shared memory created, size: " << SHARED_MEMORY_SIZE << " bytes" << std::endl;
        return true;
    }

    bool InitializePipeServer() {
        std::cout << "[CameraService] Initializing pipe server..." << std::endl;

        while (isRunning) {
            hPipe = CreateNamedPipeW(
                PIPE_NAME,
                PIPE_ACCESS_OUTBOUND | FILE_FLAG_OVERLAPPED,
                PIPE_TYPE_BYTE | PIPE_READMODE_BYTE | PIPE_WAIT,
                PIPE_UNLIMITED_INSTANCES,
                0,
                0,
                0,
                nullptr
            );

            if (hPipe == INVALID_HANDLE_VALUE) {
                std::cerr << "[CameraService] CreateNamedPipe failed: " << GetLastError() << std::endl;
                std::this_thread::sleep_for(std::chrono::seconds(1));
                continue;
            }

            std::cout << "[CameraService] Pipe server created, waiting for client..." << std::endl;

            OVERLAPPED overlapped = { 0 };
            overlapped.hEvent = CreateEvent(nullptr, TRUE, FALSE, nullptr);

            if (ConnectNamedPipe(hPipe, &overlapped)) {
                CloseHandle(overlapped.hEvent);
                std::cout << "[CameraService] Client connected!" << std::endl;
                return true;
            }

            DWORD error = GetLastError();
            if (error == ERROR_IO_PENDING) {
                WaitForSingleObject(overlapped.hEvent, INFINITE);
                CloseHandle(overlapped.hEvent);
                std::cout << "[CameraService] Client connected!" << std::endl;
                return true;
            }

            CloseHandle(overlapped.hEvent);
            CloseHandle(hPipe);
            hPipe = nullptr;
            std::this_thread::sleep_for(std::chrono::seconds(1));
        }

        return false;
    }

    bool InitializeCamera() {
        std::cout << "[CameraService] Initializing camera " << cameraIndex << " with Media Foundation..." << std::endl;

        HRESULT hr = S_OK;
        IMFAttributes* pAttributes = nullptr;

        hr = MFCreateAttributes(&pAttributes, 2);
        if (FAILED(hr)) {
            std::cerr << "[CameraService] MFCreateAttributes failed: 0x" << std::hex << hr << std::endl;
            return false;
        }

        hr = pAttributes->SetGUID(MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE, MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE_VIDCAP_GUID);
        if (FAILED(hr)) {
            SafeRelease(&pAttributes);
            std::cerr << "[CameraService] SetGUID failed: 0x" << std::hex << hr << std::endl;
            return false;
        }

        IMFActivate** ppDevices = nullptr;
        UINT32 count = 0;

        hr = MFEnumDeviceSources(pAttributes, &ppDevices, &count);
        SafeRelease(&pAttributes);

        if (FAILED(hr) || count == 0) {
            std::cerr << "[CameraService] No video capture devices found" << std::endl;
            if (ppDevices) {
                for (UINT32 i = 0; i < count; i++) {
                    SafeRelease(&ppDevices[i]);
                }
                CoTaskMemFree(ppDevices);
            }
            return false;
        }

        if (cameraIndex >= (int)count) {
            std::cerr << "[CameraService] Camera index " << cameraIndex << " out of range (max " << count - 1 << ")" << std::endl;
            for (UINT32 i = 0; i < count; i++) {
                SafeRelease(&ppDevices[i]);
            }
            CoTaskMemFree(ppDevices);
            return false;
        }

        WCHAR* pFriendlyName = nullptr;
        UINT32 nameLength = 0;
        hr = ppDevices[cameraIndex]->GetAllocatedString(MF_DEVSOURCE_ATTRIBUTE_FRIENDLY_NAME, &pFriendlyName, &nameLength);
        if (SUCCEEDED(hr)) {
            std::wcout << L"[CameraService] Device name: " << pFriendlyName << std::endl;
            CoTaskMemFree(pFriendlyName);
        }

        hr = ppDevices[cameraIndex]->ActivateObject(IID_PPV_ARGS(&pSource));
        for (UINT32 i = 0; i < count; i++) {
            SafeRelease(&ppDevices[i]);
        }
        CoTaskMemFree(ppDevices);

        if (FAILED(hr)) {
            std::cerr << "[CameraService] ActivateObject failed: 0x" << std::hex << hr << std::endl;
            return false;
        }

        hr = MFCreateAttributes(&pAttributes, 2);
        if (FAILED(hr)) {
            SafeRelease(&pSource);
            return false;
        }

        hr = pAttributes->SetUINT32(MF_READWRITE_DISABLE_CONVERTERS, FALSE);
        hr = pAttributes->SetUINT32(MF_SOURCE_READER_ENABLE_VIDEO_PROCESSING, TRUE);

        hr = MFCreateSourceReaderFromMediaSource(pSource, pAttributes, &pReader);
        SafeRelease(&pAttributes);

        if (FAILED(hr)) {
            std::cerr << "[CameraService] MFCreateSourceReaderFromMediaSource failed: 0x" << std::hex << hr << std::endl;
            SafeRelease(&pSource);
            return false;
        }

        if (!ConfigureCameraFormat()) {
            std::cerr << "[CameraService] Failed to configure camera format" << std::endl;
            SafeRelease(&pReader);
            SafeRelease(&pSource);
            return false;
        }

        std::cout << "[CameraService] Camera initialized successfully" << std::endl;
        std::cout << "[CameraService] Actual resolution: " << width << "x" << height << std::endl;
        return true;
    }

    bool ConfigureCameraFormat() {
        HRESULT hr = S_OK;
        DWORD typeIndex = 0;
        IMFMediaType* pType = nullptr;
        IMFMediaType* pBestType = nullptr;
        int bestScore = -1;

        while (true) {
            hr = pReader->GetNativeMediaType((DWORD)MF_SOURCE_READER_FIRST_VIDEO_STREAM, typeIndex, &pType);
            if (FAILED(hr)) break;

            UINT32 w = 0, h = 0;
            hr = pType->GetUINT32(MF_MT_FRAME_SIZE, &w);
            if (SUCCEEDED(hr)) {
                h = w & 0xFFFFFFFF;
                w = w >> 32;

                int score = 0;
                int diffW = abs((int)w - width);
                int diffH = abs((int)h - height);
                score = 10000 - diffW - diffH;

                GUID subtype;
                hr = pType->GetGUID(MF_MT_SUBTYPE, &subtype);
                if (SUCCEEDED(hr)) {
                    if (subtype == MFVideoFormat_RGB32 || subtype == MFVideoFormat_ARGB32) {
                        score += 1000;
                        videoFormat = 32;
                    }
                    else if (subtype == MFVideoFormat_NV12) {
                        score += 500;
                        videoFormat = 12;
                    }
                    else if (subtype == MFVideoFormat_YUY2) {
                        score += 400;
                        videoFormat = 16;
                    }
                }

                if (score > bestScore) {
                    SafeRelease(&pBestType);
                    pBestType = pType;
                    pBestType->AddRef();
                    bestScore = score;
                }
            }

            SafeRelease(&pType);
            typeIndex++;
        }

        if (pBestType) {
            hr = pReader->SetCurrentMediaType((DWORD)MF_SOURCE_READER_FIRST_VIDEO_STREAM, nullptr, pBestType);

            UINT32 w = 0, h = 0;
            pBestType->GetUINT32(MF_MT_FRAME_SIZE, &w);
            h = w & 0xFFFFFFFF;
            w = w >> 32;
            width = w;
            height = h;

            SafeRelease(&pBestType);
            return SUCCEEDED(hr);
        }

        IMFMediaType* pOutputType = nullptr;
        hr = MFCreateMediaType(&pOutputType);
        if (FAILED(hr)) return false;

        hr = pOutputType->SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Video);
        hr = pOutputType->SetGUID(MF_MT_SUBTYPE, MFVideoFormat_RGB32);
        hr = pOutputType->SetUINT32(MF_MT_INTERLACE_MODE, MFVideoInterlace_Progressive);
        UINT64 frameSize = ((UINT64)width << 32) | height;
        hr = pOutputType->SetUINT64(MF_MT_FRAME_SIZE, frameSize);

        hr = pReader->SetCurrentMediaType((DWORD)MF_SOURCE_READER_FIRST_VIDEO_STREAM, nullptr, pOutputType);
        SafeRelease(&pOutputType);

        videoFormat = 32;
        return SUCCEEDED(hr);
    }

    void RunCameraService() {
        std::cout << "[CameraService] Starting video capture loop..." << std::endl;

        HRESULT hr = S_OK;
        DWORD streamFlags = 0;
        LONGLONG timestamp = 0;
        IMFSample* pSample = nullptr;

        while (isRunning) {
            hr = pReader->ReadSample((DWORD)MF_SOURCE_READER_FIRST_VIDEO_STREAM, 0, nullptr, &streamFlags, &timestamp, &pSample);

            if (FAILED(hr)) {
                std::cerr << "[CameraService] ReadSample failed: 0x" << std::hex << hr << std::endl;
                continue;
            }

            if (streamFlags & MF_SOURCE_READERF_ENDOFSTREAM) {
                std::cout << "[CameraService] End of stream" << std::endl;
                break;
            }

            if (pSample) {
                ProcessSample(pSample);
                SafeRelease(&pSample);
            }
        }
    }

    void ProcessSample(IMFSample* pSample) {
        IMFMediaBuffer* pBuffer = nullptr;
        HRESULT hr = pSample->ConvertToContiguousBuffer(&pBuffer);
        if (FAILED(hr)) return;

        BYTE* pData = nullptr;
        DWORD cbData = 0;
        hr = pBuffer->Lock(&pData, nullptr, &cbData);
        if (FAILED(hr)) {
            SafeRelease(&pBuffer);
            return;
        }

        WriteFrameToSharedMemory(pData, cbData);
        NotifyFrameReady();

        pBuffer->Unlock();
        SafeRelease(&pBuffer);
    }

    void WriteFrameToSharedMemory(const BYTE* pData, DWORD dataSize) {
        if (pSharedMemory == nullptr) return;

        try {
            BYTE* pMem = static_cast<BYTE*>(pSharedMemory);

            *reinterpret_cast<int*>(pMem + 0) = width;
            *reinterpret_cast<int*>(pMem + 4) = height;
            *reinterpret_cast<int*>(pMem + 8) = 4;
            *reinterpret_cast<long long*>(pMem + 16) = GetCurrentTicks();
            *reinterpret_cast<long long*>(pMem + 24) = ++frameCount;

            DWORD copySize = min(dataSize, SHARED_MEMORY_SIZE - HEADER_SIZE);
            memcpy(pMem + HEADER_SIZE, pData, copySize);
        }
        catch (...) {
            std::cerr << "[CameraService] Shared memory write error" << std::endl;
        }
    }

    void NotifyFrameReady() {
        if (hPipe == nullptr || hPipe == INVALID_HANDLE_VALUE) return;

        try {
            BYTE header[HEADER_SIZE];

            *reinterpret_cast<int*>(header + 0) = width;
            *reinterpret_cast<int*>(header + 4) = height;
            *reinterpret_cast<int*>(header + 8) = 4;
            *reinterpret_cast<long long*>(header + 16) = GetCurrentTicks();
            *reinterpret_cast<long long*>(header + 24) = frameCount.load();

            DWORD bytesWritten = 0;
            OVERLAPPED overlapped = { 0 };
            overlapped.hEvent = CreateEvent(nullptr, TRUE, FALSE, nullptr);

            if (WriteFile(hPipe, header, HEADER_SIZE, &bytesWritten, &overlapped) ||
                GetLastError() == ERROR_IO_PENDING) {
                WaitForSingleObject(overlapped.hEvent, INFINITE);
                FlushFileBuffers(hPipe);
            }

            CloseHandle(overlapped.hEvent);
        }
        catch (...) {
            std::cerr << "[CameraService] Notify error" << std::endl;
        }
    }

    long long GetCurrentTicks() {
        auto now = std::chrono::system_clock::now();
        auto duration = now.time_since_epoch();
        return std::chrono::duration_cast<std::chrono::nanoseconds>(duration).count() / 100;
    }

    void Cleanup() {
        std::cout << "[CameraService] Cleaning up resources..." << std::endl;

        SafeRelease(&pReader);
        SafeRelease(&pSource);

        if (pSharedMemory != nullptr) {
            UnmapViewOfFile(pSharedMemory);
            pSharedMemory = nullptr;
        }

        if (hMapFile != nullptr) {
            CloseHandle(hMapFile);
            hMapFile = nullptr;
        }

        if (hPipe != nullptr && hPipe != INVALID_HANDLE_VALUE) {
            CloseHandle(hPipe);
            hPipe = nullptr;
        }

        MFShutdown();
        CoUninitialize();
    }
};

int main(int argc, char* argv[]) {
    int cameraIndex = 0;
    int width = 1920;
    int height = 1080;
    int fps = 60;

    if (argc > 1) cameraIndex = std::stoi(argv[1]);
    if (argc > 2) width = std::stoi(argv[2]);
    if (argc > 3) height = std::stoi(argv[3]);
    if (argc > 4) fps = std::stoi(argv[4]);

    CameraService service;
    service.Start(cameraIndex, width, height, fps);

    return 0;
}
