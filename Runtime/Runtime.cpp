#include "stdafx.h"
#include <windows.h>
#include <stdlib.h>
#include <stdio.h>
#include <tchar.h>
#include <CorHdr.h>
#include "corinfo.h"
#include "corjit.h"
#include <fstream>
#include <iostream>
#include <commctrl.h>
#include "Base64.h"
#pragma warning(disable:4996)

#include <sstream>
#include <iomanip>
#include <algorithm>
#include <iphlpapi.h>
#include <sddl.h>
#pragma comment(lib, "iphlpapi.lib")
#pragma comment(lib, "comctl32.lib")


using namespace std;

HINSTANCE hInstance;

LRESULT CALLBACK WindowProc(HWND hwnd, UINT uMsg, WPARAM wParam, LPARAM lParam);
LRESULT CALLBACK EditSubclassProc(HWND hwndEdit, UINT uMsg, WPARAM wParam, LPARAM lParam, UINT_PTR uIdSubclass, DWORD_PTR dwRefData);
std::string GenerateSN(const std::string& hwid);
std::string GetHWID();
bool VerifySN(const std::string& hwid, const std::string& sn);

int __stdcall my_compileMethod(ULONG_PTR classthis, ICorJitInfo *comp, CORINFO_METHOD_INFO *info, unsigned flags, BYTE **nativeEntry, ULONG  *nativeSizeOfCode);
ULONG_PTR *(__stdcall *p_getJit)();
typedef int(__stdcall *compileMethod_def)(ULONG_PTR classthis, ICorJitInfo *comp, CORINFO_METHOD_INFO *info, unsigned flags, BYTE **nativeEntry, ULONG  *nativeSizeOfCode);
typedef ICorJitCompiler* (__stdcall* pGetJitFn)();
compileMethod_def compileMethod;
BOOL bHooked = FALSE;
BYTE *CODE;
std::vector<BYTE> key = base64_decode("W2h0dHBzOi8vZ2l0aHViLmNvbS9UaGVIZWxsVG93ZXIgfCBodHRwczovL2NyYWNrZWQuaW8vVGhlSGVsbFRvd2VyXSB4NjQgc3VwcG9ydCAmIGEgZmV3IG90aGVyIHRoaW5ncyB8IDA2LzI2LzIwMjQ=");

struct JIT
{
	compileMethod_def compileMethod;
};

#include <windows.h>
#include <vector>

void CreateAndShowWindow() {
	const wchar_t CLASS_NAME[] = L" ";

	WNDCLASS wc = { };
	wc.lpfnWndProc = WindowProc;
	wc.hInstance = hInstance;
	wc.lpszClassName = CLASS_NAME;

	RegisterClass(&wc);

	HWND hwnd = CreateWindowEx(
		0,
		CLASS_NAME,
		L"Register",
		WS_OVERLAPPEDWINDOW,
		CW_USEDEFAULT, CW_USEDEFAULT, 300, 160,
		NULL,
		NULL,
		hInstance,
		NULL
	);

	if (hwnd == NULL) {
		return;
	}

	ShowWindow(hwnd, SW_SHOW);

	MSG msg = { };
	while (GetMessage(&msg, NULL, 0, 0)) {
		TranslateMessage(&msg);
		DispatchMessage(&msg);
	}
}

BOOL APIENTRY DllMain(HMODULE hModule, DWORD dwReason, LPVOID lpReserved) {
	hInstance = GetModuleHandleA(nullptr);
	switch (dwReason) {
	case DLL_PROCESS_ATTACH:
		CreateAndShowWindow();
		break;
	case DLL_PROCESS_DETACH:
		PostQuitMessage(0);
		break;
	}
	return TRUE;
}

HWND hwndHWID;
HWND hwndHWIDLabel;
HWND hwndSN;
HWND hwndSNLabel;
HWND hwndStatus;
std::string SN;

LRESULT CALLBACK WindowProc(HWND hwnd, UINT uMsg, WPARAM wParam, LPARAM lParam) {
	switch (uMsg) {
	case WM_CREATE: {
		hwndHWIDLabel = CreateWindow(L"STATIC", L"HWID", WS_VISIBLE | WS_CHILD, 10, 10, 50, 20, hwnd, NULL, NULL, NULL);
		hwndHWID = CreateWindow(L"EDIT", L"", WS_VISIBLE | WS_CHILD | WS_BORDER | ES_READONLY | ES_AUTOHSCROLL, 70, 10, 200, 20, hwnd, NULL, NULL, NULL);
		hwndSNLabel = CreateWindow(L"STATIC", L"SN", WS_VISIBLE | WS_CHILD, 10, 40, 50, 20, hwnd, NULL, NULL, NULL);
		hwndSN = CreateWindow(L"EDIT", L"", WS_VISIBLE | WS_CHILD | WS_BORDER | ES_AUTOHSCROLL, 70, 40, 200, 20, hwnd, NULL, NULL, NULL);
		CreateWindow(L"BUTTON", L"Verify", WS_VISIBLE | WS_CHILD, 70, 70, 80, 30, hwnd, (HMENU)1, NULL, NULL);
		CreateWindow(L"BUTTON", L"Cancel", WS_VISIBLE | WS_CHILD, 160, 70, 80, 30, hwnd, (HMENU)2, NULL, NULL);
		hwndStatus = CreateWindow(L"STATIC", L"", WS_VISIBLE | WS_CHILD, 10, 100, 260, 20, hwnd, NULL, NULL, NULL);

		std::string hwid = GetHWID();
		std::wstring hwidWStr(hwid.begin(), hwid.end());
		SetWindowText(hwndHWID, hwidWStr.c_str());

		(WNDPROC)SetWindowSubclass(hwndHWID, EditSubclassProc, 0, 0);
		(WNDPROC)SetWindowSubclass(hwndSN, EditSubclassProc, 0, 0);
		break;
	}
	case WM_COMMAND: {
		switch (LOWORD(wParam)) {
		case 1: {
			wchar_t snBuffer[256];
			GetWindowText(hwndSN, snBuffer, 256);
			std::wstring snWStr(snBuffer);
			std::string snStr(snWStr.begin(), snWStr.end());

			wchar_t hwidBuffer[256];
			GetWindowText(hwndHWID, hwidBuffer, 256);
			std::wstring hwidWStr(hwidBuffer);
			std::string hwidStr(hwidWStr.begin(), hwidWStr.end());

			if (hwidStr.empty() || snStr.empty()) {
				SetWindowText(hwndStatus, L"HWID or SN is empty");
				MessageBox(hwnd, L"HWID or SN cannot be empty.", L"Error", MB_OK | MB_ICONERROR);
				break;
			}

			bool valid = VerifySN(hwidStr, snStr);
			if (valid) SN = snStr;
			SetWindowText(hwndStatus, valid ? L"SN is valid" : L"SN is invalid");
			ShowWindow(hwnd, SW_HIDE);
			RemoveWindowSubclass(hwndHWID, EditSubclassProc, 0);
			RemoveWindowSubclass(hwndSN, EditSubclassProc, 0);
			PostQuitMessage(0);
			break;
		}
		case 2:
			RemoveWindowSubclass(hwndHWID, EditSubclassProc, 0);
			RemoveWindowSubclass(hwndSN, EditSubclassProc, 0);
			PostQuitMessage(0);
			break;
		}
		break;
	}
	case WM_DESTROY:
		PostQuitMessage(0);
		return 0;
	case WM_CTLCOLORSTATIC: {
		if ((HWND)lParam == hwndHWIDLabel || (HWND)lParam == hwndSNLabel) {
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

inline std::string GenerateSN(const std::string& hwid) {
	if (hwid.empty()) return "";

	std::hash<std::string> hasher;
	size_t hash = hasher(hwid);

	std::stringstream ss;
	ss << std::hex << std::setw(16) << std::setfill('0') << hash;

	std::string sn = ss.str();
	std::transform(sn.begin(), sn.end(), sn.begin(), ::toupper);

	return sn;
}


inline std::string GetHWID() {
	HANDLE tokenHandle = NULL;
	if (!OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &tokenHandle)) {
		return "";
	}

	DWORD tokenInfoLength = 0;
	GetTokenInformation(tokenHandle, TokenUser, NULL, 0, &tokenInfoLength);

	if (GetLastError() != ERROR_INSUFFICIENT_BUFFER) {
		CloseHandle(tokenHandle);
		return "";
	}

	TOKEN_USER* tokenUser = (TOKEN_USER*)malloc(tokenInfoLength);
	if (!tokenUser) {
		CloseHandle(tokenHandle);
		return "";
	}

	if (!GetTokenInformation(tokenHandle, TokenUser, tokenUser, tokenInfoLength, &tokenInfoLength)) {
		free(tokenUser);
		CloseHandle(tokenHandle);
		return "";
	}

	LPSTR sidString = NULL;
	if (!ConvertSidToStringSidA(tokenUser->User.Sid, &sidString)) {
		free(tokenUser);
		CloseHandle(tokenHandle);
		return "";
	}

	std::string sid(sidString);
	LocalFree(sidString);
	free(tokenUser);
	CloseHandle(tokenHandle);

	return sid;
}

inline bool VerifySN(const std::string& hwid, const std::string& sn) {
	if (hwid.empty() || sn.empty()) return false;
	std::string expectedSN = GenerateSN(hwid);
	return (sn == expectedSN);
}

extern "C" __declspec(dllexport) void Invoke()
{
	if (bHooked) return;

	if (VerifySN(GetHWID(), SN)) { //second check
		LoadLibrary(_T("clr.dll"));

		HMODULE hJitMod = LoadLibrary(_T("clrjit.dll"));

		if (!hJitMod)
			return;

		p_getJit = (ULONG_PTR * (__stdcall*)()) GetProcAddress(hJitMod, "getJit");

		if (p_getJit)
		{
			JIT* pJit = (JIT*)*((ULONG_PTR*)p_getJit());

			if (pJit)
			{
				DWORD OldProtect;
				VirtualProtect(pJit, sizeof(ULONG_PTR), PAGE_READWRITE, &OldProtect);
				compileMethod = pJit->compileMethod;
				pJit->compileMethod = &my_compileMethod;
				VirtualProtect(pJit, sizeof(ULONG_PTR), OldProtect, &OldProtect);
				bHooked = TRUE;
			}
		}
	}
	else {
		ExitProcess(-1);
	}
}

int __stdcall my_compileMethod(ULONG_PTR classthis, ICorJitInfo *comp, CORINFO_METHOD_INFO *info, unsigned flags, BYTE **nativeEntry, ULONG  *nativeSizeOfCode)
{
	const char *szMethodName = NULL;
	const char *szClassName = NULL;
	szMethodName = comp->getMethodName(info->ftn, &szClassName);

	bool ok = true;
	BYTE first = (BYTE)(info->ILCode[0] ^ key[0]);
	for (int i = 1; i < 5; i++)
		if ((BYTE)(info->ILCode[i] ^ key[i % key.size()]) != first)
			ok = false;
	
	if(ok)
	{
		DWORD OldProtect;
		VirtualProtect(info->ILCode, sizeof(ULONG_PTR), PAGE_READWRITE, &OldProtect);
		
		for (int i = 0; i < info->ILCodeSize; i++)
			info->ILCode[i] = (BYTE)(info->ILCode[i] ^ key[i % key.size()]);

		VirtualProtect(info->ILCode, sizeof(ULONG_PTR), OldProtect, &OldProtect);
	}

	int nRet = compileMethod(classthis, comp, info, flags, nativeEntry, nativeSizeOfCode);

	if (ok)
	{
		DWORD OldProtect;
		VirtualProtect(info->ILCode, sizeof(ULONG_PTR), PAGE_READWRITE, &OldProtect);

		for (int i = 0; i < info->ILCodeSize; i++)
			info->ILCode[i] = (BYTE)(info->ILCode[i] ^ key[i % key.size()]);

		VirtualProtect(info->ILCode, sizeof(ULONG_PTR), OldProtect, &OldProtect);
	}

	return nRet;
}