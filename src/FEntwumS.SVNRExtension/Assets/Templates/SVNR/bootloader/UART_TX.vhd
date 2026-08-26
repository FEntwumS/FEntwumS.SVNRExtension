--! \file UART_TX.vhd.vhd
--! \brief UART tx/transmit Komponente des Bootloaders  
--! 
--! Implementiert UART tx Protokoll.
--!	Nimmt einen Byte zu sendender Daten über `i_RX_Byte` entgegen, und gibt dieses als seriellen Datenstrom über `i_RX_Serial` aus 
--! Berechnung der Baudrate ist an 50MHz Grundtakt angepasst.

library ieee;
use ieee.std_logic_1164.all;
use ieee.numeric_std.all;

--! \brief UART tx/transmit Komponente des Bootloaders  
--! 
--! Implementiert UART tx Protokoll.
--!	Nimmt einen Byte zu sendender Daten über `i_RX_Byte` entgegen, und gibt dieses als seriellen Datenstrom über `i_RX_Serial` aus 
--! Berechnung der Baudrate ist an 50MHz Grundtakt angepasst.
entity UART_TX is
  generic (
    g_CLKS_PER_BIT : integer := 434     -- for 50MHz Clock: 12000000 / 115200
    -- g_CLKS_PER_BIT : integer := 104     -- for 12MHz Clock: 12000000 / 115200
    );
  port (
    i_Clk       : in  std_logic;
    i_TX_DV     : in  std_logic;                      --! Data Valid Pulse: Signalisiert valide Daten an `i_TX_Byte` - Startet die Uebertragung des anliegenden Bytes uber UART
    i_TX_Byte   : in  std_logic_vector(7 downto 0);   --! Zu sendende Daten (1 Byte)
    o_TX_Active : out std_logic;                      --! Transmission in Progress
    o_TX_Serial : out std_logic;                      --! Serieller Datenstrom, welcher an den UART Pingeht.
    o_TX_Done   : out std_logic                       --! Signalisiert Byte wurde erfolgreich uebertragen
    );
end UART_TX;


architecture RTL of UART_TX is

  type t_SM_Main is (IDLE, TX_START_BIT, TX_DATA_BITS,
                     TX_STOP_BIT, CLEANUP);
  signal r_SM_Main : t_SM_Main := IDLE;

  signal r_Clk_Count : integer range 0 to g_CLKS_PER_BIT-1 := 0;
  signal r_Bit_Index : integer range 0 to 7 := 0;  -- 8 Bits Total
  signal r_TX_Data   : std_logic_vector(7 downto 0) := (others => '0');
  signal r_TX_Done   : std_logic := '0';
  
begin

  
  p_UART_TX : process (i_Clk)
  begin
    if rising_edge(i_Clk) then
        
      r_TX_Done   <= '0';  -- Default assignment

      case r_SM_Main is

        when IDLE =>
          o_TX_Active <= '0';
          o_TX_Serial <= '1';         -- Drive Line High for Idle
          r_Clk_Count <= 0;
          r_Bit_Index <= 0;

          if i_TX_DV = '1' then
            r_TX_Data <= i_TX_Byte;
            r_SM_Main <= TX_START_BIT;
          else
            r_SM_Main <= IDLE;
          end if;

          
        -- Send out Start Bit. Start bit = 0
        when TX_START_BIT =>
          o_TX_Active <= '1';
          o_TX_Serial <= '0';

          -- Wait g_CLKS_PER_BIT-1 clock cycles for start bit to finish
          if r_Clk_Count < g_CLKS_PER_BIT-1 then
            r_Clk_Count <= r_Clk_Count + 1;
            r_SM_Main   <= TX_START_BIT;
          else
            r_Clk_Count <= 0;
            r_SM_Main   <= TX_DATA_BITS;
          end if;

          
        -- Wait g_CLKS_PER_BIT-1 clock cycles for data bits to finish          
        when TX_DATA_BITS =>
          o_TX_Serial <= r_TX_Data(r_Bit_Index);
          
          if r_Clk_Count < g_CLKS_PER_BIT-1 then
            r_Clk_Count <= r_Clk_Count + 1;
            r_SM_Main   <= TX_DATA_BITS;
          else
            r_Clk_Count <= 0;
            
            -- Check if we have sent out all bits
            if r_Bit_Index < 7 then
              r_Bit_Index <= r_Bit_Index + 1;
              r_SM_Main   <= TX_DATA_BITS;
            else
              r_Bit_Index <= 0;
              r_SM_Main   <= TX_STOP_BIT;
            end if;
          end if;


        -- Send out Stop bit.  Stop bit = 1
        when TX_STOP_BIT =>
          o_TX_Serial <= '1';

          -- Wait g_CLKS_PER_BIT-1 clock cycles for Stop bit to finish
          if r_Clk_Count < g_CLKS_PER_BIT-1 then
            r_Clk_Count <= r_Clk_Count + 1;
            r_SM_Main   <= TX_STOP_BIT;
          else
            r_TX_Done   <= '1';
            r_Clk_Count <= 0;
            r_SM_Main   <= CLEANUP;
          end if;

                  
        -- Stay here 1 clock
        when CLEANUP =>
          o_TX_Active <= '0';
          r_SM_Main   <= IDLE;
          
            
        when others =>
          r_SM_Main <= IDLE;

      end case;
    end if;
  end process p_UART_TX;

  o_TX_Done <= r_TX_Done;
  
end RTL;
