#pragma once

#ifndef PIPEMESSAGE_H
#define PIPEMESSAGE_H

#include "PMCommand.h"

#pragma pack(1)
class PipeMessage
{
public:
	int PID;
	PMCommand Cmd;
	char Parameter;
};

#endif // !1
