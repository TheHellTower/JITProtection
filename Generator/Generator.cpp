#include <windows.h>
#include <commctrl.h>
#include <string>
#include <sstream>
#include <iomanip>
#include <algorithm>
#pragma comment(lib, "comctl32.lib")

LRESULT CALLBACK WindowProc(HWND hwnd, UINT uMsg, WPARAM wParam, LPARAM lParam);
LRESULT CALLBACK EditSubclassProc(HWND hwndEdit, UINT uMsg, WPARAM wParam, LPARAM lParam, UINT_PTR uIdSubclass, DWORD_PTR dwRefData);
std::string GenerateSN(const std::string& hwid);

int WINAPI wWinMain(HINSTANCE hInstance, HINSTANCE, PWSTR pCmdLine, int nCmdShow) {
    const wchar_t CLASS_NAME[] = L" ";

    WNDCLASS wc = { };

    wc.lpfnWndProc = WindowProc;
    wc.hInstance = hInstance;
    wc.lpszClassName = CLASS_NAME;

    RegisterClass(&wc);

    HWND hwnd = CreateWindowEx(
        0,
        CLASS_NAME,
        L"Generator",
        WS_OVERLAPPEDWINDOW,
        CW_USEDEFAULT, CW_USEDEFAULT, 300, 150,
        NULL,
        NULL,
        hInstance,
        NULL
    );

    if (hwnd == NULL) {
        return 0;
    }

    ShowWindow(hwnd, nCmdShow);

    MSG msg = { };
    while (GetMessage(&msg, NULL, 0, 0)) {
        TranslateMessage(&msg);
        DispatchMessage(&msg);
    }

    return 0;
}

HWND hwndHWID;
HWND hwndSN;

LRESULT CALLBACK WindowProc(HWND hwnd, UINT uMsg, WPARAM wParam, LPARAM lParam) {
    switch (uMsg) {
    case WM_CREATE:
        CreateWindow(L"STATIC", L"HWID", WS_VISIBLE | WS_CHILD, 10, 10, 50, 20, hwnd, NULL, NULL, NULL);
        hwndHWID = CreateWindow(L"EDIT", L"", WS_VISIBLE | WS_CHILD | WS_BORDER | ES_AUTOHSCROLL, 70, 10, 200, 20, hwnd, NULL, NULL, NULL);
        CreateWindow(L"STATIC", L"SN", WS_VISIBLE | WS_CHILD, 10, 40, 50, 20, hwnd, NULL, NULL, NULL);
        hwndSN = CreateWindow(L"EDIT", L"", WS_VISIBLE | WS_CHILD | WS_BORDER | ES_AUTOHSCROLL, 70, 40, 200, 20, hwnd, NULL, NULL, NULL);
        CreateWindow(L"BUTTON", L"OK", WS_VISIBLE | WS_CHILD, 70, 70, 80, 30, hwnd, (HMENU)1, NULL, NULL);
        CreateWindow(L"BUTTON", L"Cancel", WS_VISIBLE | WS_CHILD, 160, 70, 80, 30, hwnd, (HMENU)2, NULL, NULL);

        (WNDPROC)SetWindowSubclass(hwndHWID, EditSubclassProc, 0, 0);
        (WNDPROC)SetWindowSubclass(hwndSN, EditSubclassProc, 0, 0);
        break;
    case WM_COMMAND:
        if (LOWORD(wParam) == 1) {
            wchar_t hwidBuffer[256];
            GetWindowText(hwndHWID, hwidBuffer, 256);
            std::wstring hwidWStr(hwidBuffer);
            std::string hwidStr(hwidWStr.begin(), hwidWStr.end());

            std::string sn = GenerateSN(hwidStr);
            std::wstring snWStr(sn.begin(), sn.end());
            SetWindowText(hwndSN, snWStr.c_str());
        }
        else if (LOWORD(wParam) == 2) {
            PostQuitMessage(0);
        }
        break;
    case WM_DESTROY:
        RemoveWindowSubclass(hwndHWID, EditSubclassProc, 0);
        RemoveWindowSubclass(hwndSN, EditSubclassProc, 0);
        PostQuitMessage(0);
        return 0;

    case WM_CTLCOLORSTATIC: {
        if ((HWND)lParam == hwndHWID || (HWND)lParam == hwndSN) {
            SetBkMode((HDC)wParam, TRANSPARENT);
            return (LRESULT)GetStockObject(HOLLOW_BRUSH);
        }
    }
    }

    return DefWindowProc(hwnd, uMsg, wParam, lParam);
}

LRESULT CALLBACK EditSubclassProc(HWND hwndEdit, UINT uMsg, WPARAM wParam, LPARAM lParam, UINT_PTR uIdSubclass, DWORD_PTR dwRefData) {
    switch (uMsg) {
    case WM_KEYDOWN: {
        if ((wParam == 'A') && (GetAsyncKeyState(VK_CONTROL) & 0x8000)) {
            SendMessage(hwndEdit, EM_SETSEL, 0, -1);
            return 0;
        }
        if ((wParam == 'C') && (GetAsyncKeyState(VK_CONTROL) & 0x8000)) {
            SendMessage(hwndEdit, WM_COPY, 0, 0);
            return 0;
        }
        break;
    }
    }

    return DefSubclassProc(hwndEdit, uMsg, wParam, lParam);
}

std::string GenerateSN(const std::string& hwid) {
    if (hwid.empty()) return "";

    std::hash<std::string> hasher;
    size_t hash = hasher(hwid);

    std::stringstream ss;
    ss << std::hex << std::setw(16) << std::setfill('0') << hash;

    std::string sn = ss.str();
    std::transform(sn.begin(), sn.end(), sn.begin(), ::toupper);

    return sn;
}