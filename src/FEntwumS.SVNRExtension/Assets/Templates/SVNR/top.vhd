--! \file top.vhd
--! \brief Top-Entit�t des gesamten Projekts - Verbindet SVNR und Bootloader
--! 
--! Verbindet alle n�tigen Ports des Bootloder mit den neu hinzugef�gten Port-Interfaces des SVNR
--! Dient au�erdem als Entrypoint f�r die in der .pcf angegebenen Pinbelegungen.
--! Es m�ssen noch Ports f�r das Memory-Mapped I/O des SVNR erzeugt werden! (LEDs, Switches, Buttons, etc.)!
--! Zugriff auf die Mempory-Mapped Hardware ist also im Moment nicht m�glich!


library IEEE;
use IEEE.STD_LOGIC_1164.all;
use ieee.numeric_std.all;

LIBRARY work;

use work.mem_buffer.all;

--! \brief Top-Entit�t des gesamten Projekts - Verbindet SVNR und Bootloader
--! 
--! Verbindet alle n�tigen Ports des Bootloder mit den neu hinzugef�gten Port-Interfaces des SVNR
--! Dient au�erdem als Entrypoint f�r die in der .pcf angegebenen Pinbelegungen.
--! Es m�ssen noch Ports f�r das Memory-Mapped I/O des SVNR erzeugt werden! (LEDs, Switches, Buttons, etc.)!
--! Zugriff auf die Mempory-Mapped Hardware ist also im Moment nicht m�glich!
entity top is
	port (
		clk 		: in std_logic;
		rxd   		: in std_logic;
		txd			: out std_logic
	);
end top;

architecture ARCH of top is
    signal r_cpu_en             : std_logic := '0';
    signal r_ram_address        : std_logic_vector(15 downto 0) := x"0000";
    signal r_ram_data_in        : std_logic_vector(15 downto 0);
    signal r_ram_data_out        : std_logic_vector(15 downto 0);
    signal r_wnr                : std_logic := '1';
    signal r_addrstrb           : std_logic;
    signal s_program_counter    : std_logic_vector(15 downto 0);
    signal s_ws2812_out         : std_logic;
    signal s_btn                : std_logic_vector(4 downto 0) :="00000";
    signal s_sw                 : std_logic_vector(1 downto 0) :="00";
    signal s_zehner             : std_logic_vector(3 downto 0);
    signal s_einer              : std_logic_vector(3 downto 0);
    signal s_cpu_step_fin       : std_logic;
    signal s_svnr_reset         : std_logic;
    
    signal s_register_data_svnr_in   : std_logic_vector(15 downto 0);
    signal s_register_load   : std_logic;
    signal s_register_sel   : std_logic_vector(2 downto 0);
    signal s_register_data_svnr_out   : std_logic_vector(15 downto 0);

    begin
    SVNR : entity work.svnr
        port map (
            clk             => clk,
            ws2812_out      => s_ws2812_out,
            btn             => s_btn,
            sw              => s_sw,
            zehner          => s_zehner,
            einer           => s_einer,
            -- cpu run control
            program_counter => s_program_counter,
            cpu_step_fin    => s_cpu_step_fin,
            reset_ext       => s_svnr_reset,
            cpu_en          => r_cpu_en,
            -- ram access
            ram_address_ext => r_ram_address,
            ram_data_in_ext => r_ram_data_in,
            wnr_ext         => r_wnr,
            addrstrb_ext    => r_addrstrb,
            ram_data_out_ext    => r_ram_data_out,
            -- register access
            register_data_in_ext    => s_register_data_svnr_in,
            register_load_in_ext    => s_register_load,
            register_sel_in_ext    => s_register_sel,
            register_data_out_ext    => s_register_data_svnr_out
        );

    BOOTLOADER : entity work.bootloader_top
        port map (
            i_Clk				=> clk,
            i_rxd				=> rxd,
            o_txd				=> txd,
            -- cpu run control
            i_program_counter	=> s_program_counter,
            i_cpu_step_fin      => s_cpu_step_fin,
            o_svnr_cpu_en      	=> r_cpu_en,
            o_svnr_reset        => s_svnr_reset,            
            -- ram access
            o_svnr_ram_address	=> r_ram_address(15 downto 0),
            o_svnr_ram_data 	=> r_ram_data_in(15 downto 0),
            o_svnr_wnr			=> r_wnr,
            o_svnr_addrstrb		=> r_addrstrb,
            i_svnr_ram_data     => r_ram_data_out,
            -- register access
            o_register_data    => s_register_data_svnr_in,
            o_register_load    => s_register_load,
            o_register_sel    => s_register_sel,
            i_register_data    => s_register_data_svnr_out
        );
end ARCH;
