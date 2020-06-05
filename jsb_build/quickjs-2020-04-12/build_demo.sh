#!/bin/sh

make libquickjs.dll
make examples/demo.exe
if [ $? -eq 0 ]; then
    echo "build succeed\n"
    ./examples/demo.exe
else
    echo "build failed"
fi
