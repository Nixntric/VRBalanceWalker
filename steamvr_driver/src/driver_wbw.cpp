//----------------------------------------------------------------------------------------------------------------------+
// SteamVR driver for WiiBalanceWalker.
//
// Adds one untracked controller with the Treadmill role, so games see the balance board as a locomotion device that
// can be bound to their move/turn actions in the SteamVR binding UI. WiiBalanceWalker feeds it over localhost UDP:
//
//   "WBW1" | float32 x | float32 y | float32 turn | uint8 button      (little endian, 17 bytes)
//
// x/turn: -1 left .. 1 right, y: -1 back .. 1 forward. Inputs drop to zero if no packet arrives for half a second,
// so closing WiiBalanceWalker or losing the board never leaves you walking.
//----------------------------------------------------------------------------------------------------------------------+

#define WIN32_LEAN_AND_MEAN
#include <winsock2.h>
#include <windows.h>
#include <chrono>
#include <cstring>
#include "openvr_driver.h"

#pragma comment(lib, "ws2_32.lib")

using namespace vr;

static const int    kDefaultPort = 26790;
static const double kTimeoutSec  = 0.5;

class BoardDevice : public ITrackedDeviceServerDriver
{
public:
    EVRInitError Activate(TrackedDeviceIndex_t index) override
    {
        m_index = index;
        auto props     = VRProperties();
        auto container = props->TrackedDeviceToPropertyContainer(index);

        props->SetStringProperty(container, Prop_ModelNumber_String,        "Wii Balance Board");
        props->SetStringProperty(container, Prop_ManufacturerName_String,   "WiiBalanceWalker");
        props->SetStringProperty(container, Prop_TrackingSystemName_String, "wbw");
        props->SetStringProperty(container, Prop_ControllerType_String,     "wbw_board");
        props->SetStringProperty(container, Prop_InputProfilePath_String,   "{wbw}/input/wbw_board_profile.json");
        props->SetInt32Property (container, Prop_ControllerRoleHint_Int32,  TrackedControllerRole_Treadmill);
        props->SetInt32Property (container, Prop_DeviceClass_Int32,         TrackedDeviceClass_Controller);
        props->SetBoolProperty  (container, Prop_NeverTracked_Bool,         true);
        props->SetBoolProperty  (container, Prop_WillDriftInYaw_Bool,       false);
        props->SetBoolProperty  (container, Prop_DeviceProvidesBatteryStatus_Bool, false);

        // SteamVR's status window reads these paths once, when the device appears, so they can't be swapped later.
        // Instead the device reports itself disconnected (Off: outline icon) until WiiBalanceWalker sends, and
        // connected (Ready: filled icon) while packets keep arriving -- see SetLive() and GetPose().
        props->SetStringProperty(container, Prop_NamedIconPathDeviceOff_String,                 "{wbw}/icons/wbw_status_standby.png");
        props->SetStringProperty(container, Prop_NamedIconPathDeviceSearching_String,           "{wbw}/icons/wbw_status_standby.png");
        props->SetStringProperty(container, Prop_NamedIconPathDeviceSearchingAlert_String,      "{wbw}/icons/wbw_status_standby.png");
        props->SetStringProperty(container, Prop_NamedIconPathDeviceReady_String,               "{wbw}/icons/wbw_status_on.png");
        props->SetStringProperty(container, Prop_NamedIconPathDeviceReadyAlert_String,          "{wbw}/icons/wbw_status_on.png");
        props->SetStringProperty(container, Prop_NamedIconPathDeviceNotReady_String,            "{wbw}/icons/wbw_status_error.png");
        props->SetStringProperty(container, Prop_NamedIconPathDeviceStandby_String,             "{wbw}/icons/wbw_status_on.png");
        props->SetStringProperty(container, Prop_NamedIconPathDeviceAlertLow_String,            "{wbw}/icons/wbw_status_on.png");
        m_live = false;

        auto input = VRDriverInput();
        input->CreateScalarComponent(container, "/input/joystick/x", &m_joyX,  VRScalarType_Absolute, VRScalarUnits_NormalizedTwoSided);
        input->CreateScalarComponent(container, "/input/joystick/y", &m_joyY,  VRScalarType_Absolute, VRScalarUnits_NormalizedTwoSided);
        input->CreateScalarComponent(container, "/input/turn/x",     &m_turnX, VRScalarType_Absolute, VRScalarUnits_NormalizedTwoSided);
        input->CreateScalarComponent(container, "/input/turn/y",     &m_turnY, VRScalarType_Absolute, VRScalarUnits_NormalizedTwoSided);
        input->CreateBooleanComponent(container, "/input/a/click",   &m_button);
        return VRInitError_None;
    }

    void Deactivate() override { m_index = k_unTrackedDeviceIndexInvalid; }
    void EnterStandby() override {}
    void *GetComponent(const char *) override { return nullptr; }
    void DebugRequest(const char *, char *response, uint32_t size) override { if (size) response[0] = 0; }

    DriverPose_t GetPose() override
    {
        DriverPose_t pose = {};
        pose.qWorldFromDriverRotation.w = 1;
        pose.qDriverFromHeadRotation.w  = 1;
        pose.qRotation.w                = 1;
        pose.result             = TrackingResult_Running_OK;
        pose.poseIsValid        = false;
        pose.deviceIsConnected  = m_live;
        return pose;
    }

    void SetLive(bool live)
    {
        m_live = live;      // reported through deviceIsConnected in GetPose()
    }

    void Update(float x, float y, float turn, bool button)
    {
        if (m_index == k_unTrackedDeviceIndexInvalid) return;

        VRServerDriverHost()->TrackedDevicePoseUpdated(m_index, GetPose(), sizeof(DriverPose_t));

        auto input = VRDriverInput();
        input->UpdateScalarComponent(m_joyX,  x,    0);
        input->UpdateScalarComponent(m_joyY,  y,    0);
        input->UpdateScalarComponent(m_turnX, turn, 0);
        input->UpdateScalarComponent(m_turnY, 0,    0);
        input->UpdateBooleanComponent(m_button, button, 0);
    }

private:
    TrackedDeviceIndex_t     m_index  = k_unTrackedDeviceIndexInvalid;
    bool                     m_live   = false;
    VRInputComponentHandle_t m_joyX  = k_ulInvalidInputComponentHandle;
    VRInputComponentHandle_t m_joyY   = k_ulInvalidInputComponentHandle;
    VRInputComponentHandle_t m_turnX  = k_ulInvalidInputComponentHandle;
    VRInputComponentHandle_t m_turnY  = k_ulInvalidInputComponentHandle;
    VRInputComponentHandle_t m_button = k_ulInvalidInputComponentHandle;
};

class WbwProvider : public IServerTrackedDeviceProvider
{
public:
    EVRInitError Init(IVRDriverContext *context) override
    {
        VR_INIT_SERVER_DRIVER_CONTEXT(context);

        int port = VRSettings()->GetInt32("driver_wbw", "port");
        if (port <= 0 || port > 65535) port = kDefaultPort;

        WSADATA wsa;
        if (WSAStartup(MAKEWORD(2, 2), &wsa) != 0) return VRInitError_Driver_Failed;

        m_socket = socket(AF_INET, SOCK_DGRAM, IPPROTO_UDP);
        if (m_socket == INVALID_SOCKET) return VRInitError_Driver_Failed;

        sockaddr_in addr = {};
        addr.sin_family      = AF_INET;
        addr.sin_port        = htons((u_short)port);
        addr.sin_addr.s_addr = htonl(INADDR_LOOPBACK);
        if (bind(m_socket, (sockaddr *)&addr, sizeof(addr)) != 0)
        {
            VRDriverLog()->Log("wbw: could not bind UDP port (in use?)");
            closesocket(m_socket);
            m_socket = INVALID_SOCKET;
            return VRInitError_Driver_Failed;
        }
        u_long nonBlocking = 1;
        ioctlsocket(m_socket, FIONBIO, &nonBlocking);

        char msg[96];
        snprintf(msg, sizeof(msg), "wbw: listening on 127.0.0.1:%d", port);
        VRDriverLog()->Log(msg);

        VRServerDriverHost()->TrackedDeviceAdded("WBW-BALANCE-BOARD", TrackedDeviceClass_Controller, &m_device);
        return VRInitError_None;
    }

    void Cleanup() override
    {
        if (m_socket != INVALID_SOCKET) closesocket(m_socket);
        m_socket = INVALID_SOCKET;
        WSACleanup();
        VR_CLEANUP_SERVER_DRIVER_CONTEXT();
    }

    const char *const *GetInterfaceVersions() override { return k_InterfaceVersions; }

    void RunFrame() override
    {
        // Drain everything queued and keep only the newest packet.
        char buf[64];
        int  len;
        while ((len = recv(m_socket, buf, sizeof(buf), 0)) > 0)
        {
            if (len < 17 || memcmp(buf, "WBW1", 4) != 0) continue;
            memcpy(&m_x,    buf + 4,  4);
            memcpy(&m_y,    buf + 8,  4);
            memcpy(&m_turn, buf + 12, 4);
            m_button   = buf[16] != 0;
            m_lastRecv = std::chrono::steady_clock::now();
        }

        bool fresh = std::chrono::duration<double>(std::chrono::steady_clock::now() - m_lastRecv).count() < kTimeoutSec;
        if (!fresh) { m_x = m_y = m_turn = 0; m_button = false; }

        m_device.SetLive(fresh);
        m_device.Update(Clamp(m_x), Clamp(m_y), Clamp(m_turn), m_button);

        VREvent_t event;
        while (VRServerDriverHost()->PollNextEvent(&event, sizeof(event))) {}
    }

    bool ShouldBlockStandbyMode() override { return false; }
    void EnterStandby() override {}
    void LeaveStandby() override {}

private:
    static float Clamp(float v) { return v != v ? 0.0f : v < -1 ? -1.0f : v > 1 ? 1.0f : v; }

    BoardDevice m_device;
    SOCKET      m_socket = INVALID_SOCKET;
    float       m_x = 0, m_y = 0, m_turn = 0;
    bool        m_button = false;
    std::chrono::steady_clock::time_point m_lastRecv;
};

static WbwProvider g_provider;

extern "C" __declspec(dllexport) void *HmdDriverFactory(const char *interfaceName, int *returnCode)
{
    if (strcmp(interfaceName, IServerTrackedDeviceProvider_Version) == 0) return &g_provider;
    if (returnCode) *returnCode = VRInitError_Init_InterfaceNotFound;
    return nullptr;
}
