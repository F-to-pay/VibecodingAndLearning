add_executable(sandbox main.cpp)
target_link_libraries(sandbox PRIVATE kostyl)
kostyl_setup_target(sandbox)

# На MinGW линкуем рантайм статически: exe запустится двойным кликом,
# без libstdc++-6.dll / libgcc_s_seh-1.dll рядом.
if(MINGW)
    target_link_options(sandbox PRIVATE -static)
endif()
