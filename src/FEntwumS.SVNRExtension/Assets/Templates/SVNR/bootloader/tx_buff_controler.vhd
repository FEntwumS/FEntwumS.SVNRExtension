library ieee;
use ieee.std_logic_1164.all;

entity tx_buff_controler is
    port (
		--Input
        i_Clk               : in  std_logic;
        i_ram_uploading     : in  std_logic;
        i_tx_rdy_to_fetch   : in  std_logic;
        i_TX_Done           : in  std_logic;
        i_data_out          : in  std_logic_vector(23 downto 0);
		--Output
        o_TX_Byte           : out  std_logic_vector(7 downto 0);
        o_tx_packet_done    : out  std_logic;
        o_Tx_DV             : out  std_logic
    );
end entity tx_buff_controler;

architecture Behavioral of tx_buff_controler is

signal s_tx_packet_byte_cnt : natural range 0 to 2 := 0;
signal s_TX_Byte           : std_logic_vector(7 downto 0);
type STATE is (
		z_tx_start
		, z_tx_byte
		, z_count
	);
signal fsm_state : STATE := z_tx_start;

begin 
	s_TX_Byte <= i_data_out(23 downto 16) when s_tx_packet_byte_cnt = 0 else 
		i_data_out(15 downto 8) when s_tx_packet_byte_cnt = 1 else
		i_data_out(7 downto 0);
		
	o_TX_Byte <=  s_TX_Byte;

	-- Statemachine zur Koordination des Versenden eines Bootloader Response Pakets
	fsm : process (i_Clk)
	begin
		if rising_edge(i_Clk) then
			case fsm_state is
				when z_tx_start =>
					-- if w_ram_uploading = '0' and (w_flush = '1' or s_tx_trig = '1' or tx_packet_byte_cnt >= 1) then
					if (i_tx_rdy_to_fetch = '1' or s_tx_packet_byte_cnt >= 1) then
					-- if i_ram_uploading = '0' and (i_tx_rdy_to_fetch = '1' or s_tx_packet_byte_cnt >= 1) then
						o_tx_packet_done <= '0';
						o_Tx_DV <= '1';
						fsm_state <= z_tx_byte;
					end if;

				when z_tx_byte =>
					o_Tx_DV <= '0';
					if i_TX_Done = '1' then
						fsm_state <= z_count;
					end if;
					
				when z_count =>
					-- r_Tx_DV <= '0';
					if s_tx_packet_byte_cnt >= 2 then
						s_tx_packet_byte_cnt <= 0;
						o_tx_packet_done <= '1';
						fsm_state <= z_tx_start;
					else
						s_tx_packet_byte_cnt <= s_tx_packet_byte_cnt + 1;
						fsm_state <= z_tx_start;
					end if;
			end case;
		end if;
	end process fsm;

end Behavioral;