// dllmain.cpp : Defines the entry point for the DLL application.
#include "stdafx.h"
#include "PipeMessage.h"
#include <Psapi.h>
#include <vector>
#include <iostream>
//#include <conio.h>
//#include <chrono>

using namespace std;

DWORD WINAPI MyThread(LPVOID);
DWORD g_threadID;
DWORD_PTR procAddr;
HMODULE g_hModule;
HANDLE pipe;
const char* PerformanceNoteFuncSignature = "\x48\x89\x5c\x24\x00\x57\x48\x83\xec\x00\x8b\xfa\x48\x8b\xd9\xb2";
const char* PerformanceNoteFuncSigMask = "xxxx?xxxx?xxxxxx";
const char* PerformanceNoteP1Signature = "\x48\x89\x6c\x24\x00\x56\x57\x41\x56\x48\x81\xec\x00\x00\x00\x00\x44\x0f\xb6\x35";
const char* PerformanceNoteP1SigMask = "xxxx?xxxxxxx????xxxx";
const char* SlashInstanceFuncSignature = "\x40\x53\x48\x83\xec\x00\x49\x8b\xd8\x4d\x85\xc0\x75\x00\x41\x8d\x40\x00\x48\x83\xc4\x00\x5b\xc3\x48\x8d\x0d";
const char* SlashInstanceFuncSigMask = "xxxxx?xxxxxxx?xxx?xxx?xxxxx";
const char* SlashInstanceP1Signature = "\x48\x8b\x05\x00\x00\x00\x00\x80\x78\x00\x00\x75\x00\x48\x8b\x0d";
const char* SlashInstanceP1SigMask = "xxx????xx??x?xxx";
const char* SlashInstanceP2Signature = "\x48\x8b\xf9\x48\x8b\x1d\x00\x00\x00\x00\x48\x8b\x35";
const char* SlashInstanceP2SigMask = "xxxxxx????xxx";
const char* SlashInstanceP3Signature = "\x85\xc0\x0f\x84\x00\x00\x00\x00\x80\x3d\x00\x00\x00\x00\x00\x75\x00\x80\x3d\x00\x00\x00\x00\x00\x77\x00\x48\x8b\x05";
const char* SlashInstanceP3SigMask = "xxxx????xx?????x?xx?????x?xxx";
uintptr_t PerformanceNoteFuncAddress;
uintptr_t PerformanceNoteP1Address;
const unsigned char PerformanceNoteP1Offset = 0xAF;
uintptr_t SlashInstanceFuncAddress;
uintptr_t* SlashInstancePAddress = new uintptr_t[3];
void CallPerformanceNote(char);
void CallSlashInstance();
DWORD_PTR GetProcessBaseAddress(DWORD);
uintptr_t ResolvePointerPath(uintptr_t, vector<unsigned int>);
int ConnectToPipe();
int ReadPipe(PipeMessage&);
DWORD GetModuleSize(DWORD);
uintptr_t FindPattern(uintptr_t, DWORD,const char[],const char[], bool = false);

BOOL APIENTRY DllMain(HMODULE hModule, DWORD  ul_reason_for_call, LPVOID lpReserved)
{
	switch (ul_reason_for_call)
	{
	case DLL_PROCESS_ATTACH:
		g_hModule = hModule;
		DisableThreadLibraryCalls(hModule);
		CreateThread(NULL, NULL, &MyThread, NULL, NULL, &g_threadID);
		break;
	case DLL_THREAD_ATTACH:
	case DLL_THREAD_DETACH:
		break;
	case DLL_PROCESS_DETACH:
		break;
	}
	return TRUE;
}

DWORD WINAPI MyThread(LPVOID)
{
	//AllocConsole();
	//FILE* fp;
	int PID = GetCurrentProcessId();
	procAddr = GetProcessBaseAddress(PID);
	DWORD moduleSize = GetModuleSize(PID);
	DWORD mode = PIPE_READMODE_MESSAGE;
	//freopen_s(&fp, "CONOUT$", "w", stdout);
	//cout << "CONSOLE OUTPUT" << endl;
	/*typedef chrono::high_resolution_clock clock;
	auto start_time = clock::now();*/
	PerformanceNoteFuncAddress = FindPattern(procAddr, moduleSize, PerformanceNoteFuncSignature, PerformanceNoteFuncSigMask);
	PerformanceNoteP1Address = FindPattern(procAddr, moduleSize, PerformanceNoteP1Signature, PerformanceNoteP1SigMask, true);
	SlashInstanceFuncAddress = FindPattern(procAddr, moduleSize, SlashInstanceFuncSignature, SlashInstanceFuncSigMask);
	SlashInstancePAddress[0] = FindPattern(procAddr, moduleSize, SlashInstanceP1Signature, SlashInstanceP1SigMask, true);
	SlashInstancePAddress[1] = FindPattern(procAddr, moduleSize, SlashInstanceP2Signature, SlashInstanceP2SigMask, true);
	SlashInstancePAddress[2] = FindPattern(procAddr, moduleSize, SlashInstanceP3Signature, SlashInstanceP3SigMask, true);
	/*auto end_time = clock::now();
	auto milliseconds = chrono::duration_cast<chrono::milliseconds>(end_time - start_time).count();
	cout << dec << milliseconds << "ms" << endl;
	cout << "PerformanceNoteFuncAddress = " << hex << PerformanceNoteFuncAddress << endl;
	cout << "PerformanceNoteP1Address = " << PerformanceNoteP1Address + PerformanceNoteP1Offset << endl;
	cout << "SlashInstanceFuncAddress = " << SlashInstanceFuncAddress << endl;
	cout << "SlashInstancePAddress[0] = " << SlashInstancePAddress[0] << endl;
	cout << "SlashInstancePAddress[1] = " << SlashInstancePAddress[1] << endl;
	cout << "SlashInstancePAddress[2] = " << SlashInstancePAddress[2] << endl;
	cout << "Connecting to pipe..." << endl;*/
	//CallPerformanceNote(0x3C);
	if (ConnectToPipe() == 0)
	{
		cout << pipe << endl;
		SetNamedPipeHandleState(pipe, &mode, nullptr, nullptr);
		cout << "Reading data from pipe..." << endl;
		PipeMessage lastRecMsg;
		while (ReadPipe(lastRecMsg) != 0)
		{
			if (lastRecMsg.Cmd == Exit)
				break;
			else if (lastRecMsg.PID == PID)
			{
				if (lastRecMsg.Cmd == SlashInstance) {
					CallSlashInstance();
				}
				else if (lastRecMsg.Cmd == PlayNote) {
					CallPerformanceNote(lastRecMsg.Parameter);
				}
			}
		}
		CloseHandle(pipe);
	}
	//system("pause");
	//fclose(fp);
	//FreeConsole();
	FreeLibraryAndExitThread(g_hModule, 0);
	//return 0;
}

int ReadPipe(PipeMessage& lastmsg)
{
	char buffer[128];
	DWORD numBytesRead;
	int result = ReadFile(
		pipe,
		buffer, //the data from the pipe will be put here
		127 * sizeof(char), //number of bytes allocated
		&numBytesRead, //this will store number of bytes actually read
		NULL //not using overlapped IO
	);
	if (result) {
		buffer[numBytesRead / sizeof(char)] = '\0'; // null terminate the string
		PipeMessage* msgp = (PipeMessage*)buffer;
		//cout << "Number of bytes read: " << numBytesRead << endl;
		cout << "PipeMessage.PID: " << msgp->PID << endl;
		cout << "PipeMessage.Cmd: " << msgp->Cmd << endl;
		if (msgp->Cmd == PlayNote)
			cout << "PipeMessage.Parameter: " << msgp->Parameter << endl;
		lastmsg = *msgp;
	}
	else {
		cout << "Failed to read data from the pipe." << endl;
	}
	return result;
}

int ConnectToPipe()
{
	// Try to open a named pipe; wait for it, if necessary. 
	LPTSTR lpszPipename = TEXT("\\\\.\\pipe\\XIV-Hunt");
	while (1)
	{
		pipe = CreateFile(
			lpszPipename,   //pipe name 
			GENERIC_READ,	//read access 
			0,              //no sharing 
			NULL,           //default security attributes
			OPEN_EXISTING,  //opens existing pipe 
			0,              //default attributes 
			NULL);          //no template file 

		//Break if the pipe handle is valid. 
		if (pipe != INVALID_HANDLE_VALUE)
			break;

		// Exit if an error other than ERROR_PIPE_BUSY occurs. 
		if (pipe == INVALID_HANDLE_VALUE)
		{
			cout << "INVALID_HANDLE_VALUE: " << GetLastError() << endl;
			return -1;
		}

		// All pipe instances are busy, so wait for 20 seconds. 
		if (!WaitNamedPipe(lpszPipename, 20000))
		{
			cout << "Could not open pipe: 20 second wait timed out." << endl;
			return -1;
		}
	}
	return 0;
}

uintptr_t ResolvePointerPath(uintptr_t b, vector<unsigned int> offsets = { 0x0 })
{
	for (auto &i : offsets)
		b = *reinterpret_cast<uintptr_t *>(b) + i;
	return b;
}

void CallPerformanceNote(char noteID)
{
	if (PerformanceNoteFuncAddress == NULL || PerformanceNoteP1Address == NULL ||
		noteID < 0x18 || noteID > 0x3C) //game does not have these notes
		return;
	typedef char(__fastcall *pFunctionAddress)(uintptr_t, char);
	pFunctionAddress PerformanceNoteFunctionPointer = (pFunctionAddress)(PerformanceNoteFuncAddress);
	PerformanceNoteFunctionPointer(PerformanceNoteP1Address + PerformanceNoteP1Offset, noteID);
}

void CallSlashInstance()
{
	if (SlashInstanceFuncAddress == NULL || SlashInstancePAddress[0] == NULL || SlashInstancePAddress[1] == NULL || SlashInstancePAddress[2] == NULL)
		return;
	uintptr_t RCX, RDX, R8;
	RCX = ResolvePointerPath(SlashInstancePAddress[0], { 0x90, 0x2FA8, 0x0 });
	RDX = ResolvePointerPath(SlashInstancePAddress[1], { 0x30, 0x8, 0x400 });
	R8 = ResolvePointerPath(SlashInstancePAddress[2], { 0x8, 0x0 });
	typedef __int64(__fastcall *pFunctionAddress)(uintptr_t, uintptr_t, uintptr_t);
	pFunctionAddress SlashInstanceFunctionPointer = (pFunctionAddress)(SlashInstanceFuncAddress);
	SlashInstanceFunctionPointer(RCX, RDX, R8);
}

DWORD_PTR GetProcessBaseAddress(DWORD processID)
{
	DWORD_PTR   baseAddress = 0;
	HANDLE      processHandle = OpenProcess(PROCESS_ALL_ACCESS, FALSE, processID);
	HMODULE     *moduleArray;
	LPBYTE      moduleArrayBytes;
	DWORD       bytesRequired;

	if (processHandle)
	{
		if (EnumProcessModules(processHandle, NULL, 0, &bytesRequired))
		{
			if (bytesRequired)
			{
				moduleArrayBytes = (LPBYTE)LocalAlloc(LPTR, bytesRequired);
				if (moduleArrayBytes)
				{
					unsigned int moduleCount;
					moduleCount = bytesRequired / sizeof(HMODULE);
					moduleArray = (HMODULE *)moduleArrayBytes;
					if (EnumProcessModules(processHandle, moduleArray, bytesRequired, &bytesRequired))
					{
						baseAddress = (DWORD_PTR)moduleArray[0];
					}
					LocalFree(moduleArrayBytes);
				}
			}
		}
		CloseHandle(processHandle);
	}
	return baseAddress;
}

DWORD GetModuleSize(DWORD processID)
{
	HANDLE      processHandle = OpenProcess(PROCESS_ALL_ACCESS, FALSE, processID);
	MODULEINFO mi;
	GetModuleInformation(processHandle, GetModuleHandle(L"ffxiv_dx11.exe"), &mi, sizeof(mi)+4);//+4 to prevent ERROR_INSUFFICIENT_BUFFER
	return mi.SizeOfImage;
}

uintptr_t FindPattern(uintptr_t base, DWORD size,const char pattern[],const char mask[], bool follow1st)
{
	for (uintptr_t retAddress = base; retAddress < (base + size); retAddress++)
	{
		if (*(BYTE*)retAddress == (pattern[0] & 0xff) || mask[0] == '?')
		{
			uintptr_t startSearch = retAddress;
			for (int i = 0; mask[i] != '\0'; i++, startSearch++)
			{
				if (mask[i] == '?')
					continue;
				if ((pattern[i] & 0xff) != *(BYTE*)startSearch)
					break;
				if ((pattern[i] & 0xff) == *(BYTE*)startSearch && mask[i + 1] == '\0')
				{
					if (follow1st)
					{
						uintptr_t foundAt = retAddress + i + 1;
						uintptr_t item = foundAt + 4 + *reinterpret_cast<unsigned int*>(foundAt);
						return item;
					}
					return retAddress;
				}
			}
		}
	}
	return NULL;
}