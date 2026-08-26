--! \file UART_RX.vhd.vhd
--! \brief UART rx/Receive Komponente des Bootloaders  
--! 
--! Implementiert UART rx Protokoll.
--!	Nimmt einen Seriellen Datenstrom über `i_RX_Serial` entgegen, und gibt das empfangene Byte an `o_RX_Byte` aus 
--! Berechnung der Baudrate ist an 50MHz Grundtakt angepasst.
--!

library ieee;
use ieee.std_logic_1164.ALL;
use ieee.numeric_std.all;

--! \brief UART rx/Receive Komponente des Bootloaders  
--! 
--! Implementiert UART rx Protokoll.
--!	Nimmt einen Seriellen Datenstrom über `i_RX_Serial` entgegen, und gibt das empfangene Byte an `o_RX_Byte` aus 
--! Berechnung der Baudrate ist an 50MHz Grundtakt angepasst.
--!
entity UART_RX is
  generic (
    g_CLKS_PER_BIT : integer := 434  -- for 50MHz Clock: 50000000 / 115200 
    -- g_CLKS_PER_BIT : integer := 104     -- for 12MHz Clock: 12000000 / 115200
    );
  port (
    i_Clk       : in  std_logic;
    i_RX_Serial : in  std_logic;
    o_RX_DV     : out std_logic; --! Data Valid Pulse: Signalisiert dass RX_Byte valide Daten hat (1 Byte vollstaendig per UART empfangen worden ist)
    o_RX_Byte   : out std_logic_vector(7 downto 0)
    );
end UART_RX;


architecture RTL of UART_RX is

  type t_SM_Main is (s_Idle, s_RX_Start_Bit, s_RX_Data_Bits,
                     s_RX_Stop_Bit, s_Cleanup);
  signal r_SM_Main : t_SM_Main := s_Idle;

  signal r_Clk_Count : integer range 0 to g_CLKS_PER_BIT-1 := 0;
  signal r_Bit_Index : integer range 0 to 7 := 0;
  signal r_RX_Byte   : std_logic_vector(7 downto 0) := (others => '0');
  signal r_RX_DV     : std_logic := '0';
  
begin

  p_UART_RX : process (i_Clk)
  begin

    if rising_edge(i_Clk) then       
      case r_SM_Main is
        when s_Idle =>
          r_RX_DV     <= '0';
          r_Clk_Count <= 0;
          r_Bit_Index <= 0;

		  -- check if the data transmission line is low
          if i_RX_Serial = '0' then
            r_SM_Main <= s_RX_Start_Bit; -- change state to received start bit
          else
            r_SM_Main <= s_Idle; -- remain in the idle state
          end if;
        
        when s_RX_Start_Bit =>
          if r_Clk_Count = (g_CLKS_PER_BIT-1)/2 then -- Check middle of start bit to make sure it's still low
            if i_RX_Serial = '0' then
              r_Clk_Count <= 0;  -- reset counter since we found the middle
              r_SM_Main   <= s_RX_Data_Bits;
            else
              r_SM_Main   <= s_Idle;
            end if;
          else
            r_Clk_Count <= r_Clk_Count + 1;
            r_SM_Main   <= s_RX_Start_Bit;
          end if;

          
        -- Wait g_CLKS_PER_BIT-1 clock cycles to sample serial data
        when s_RX_Data_Bits =>
          if r_Clk_Count < g_CLKS_PER_BIT-1 then
            r_Clk_Count <= r_Clk_Count + 1;
            r_SM_Main   <= s_RX_Data_Bits;
          else
            r_Clk_Count            <= 0;
            r_RX_Byte(r_Bit_Index) <= i_RX_Serial;
            
            -- Check if we have received all bits
            if r_Bit_Index < 7 then
              r_Bit_Index <= r_Bit_Index + 1;
              r_SM_Main   <= s_RX_Data_Bits;
            else
              r_Bit_Index <= 0;
              r_SM_Main   <= s_RX_Stop_Bit;
            end if;
          end if;


        -- Receive Stop bit.  Stop bit = 1
        when s_RX_Stop_Bit =>
          -- Wait g_CLKS_PER_BIT-1 clock cycles for Stop bit to finish
          if r_Clk_Count < g_CLKS_PER_BIT-1 then
            r_Clk_Count <= r_Clk_Count + 1;
            r_SM_Main   <= s_RX_Stop_Bit;
          else
            r_RX_DV     <= '1';
            r_Clk_Count <= 0;
            r_SM_Main   <= s_Cleanup;
          end if;

                  
        -- Stay here 1 clock
        when s_Cleanup =>
          r_SM_Main <= s_Idle;
          r_RX_DV   <= '0';

            
        when others =>
          r_SM_Main <= s_Idle;

      end case;
    end if;
  end process p_UART_RX;

  o_RX_DV   <= r_RX_DV;
  o_RX_Byte <= r_RX_Byte;
  
end RTL;