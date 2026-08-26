library ieee;
use ieee.std_logic_1164.all;

entity svnr_en_controller is
    port (
        i_Clk           	: in  std_logic;
        i_cm_run   			: in  std_logic;
        i_bp_run	    	: in  std_logic;
		i_single_step_en	: in  std_logic;
		i_step_finished		: in  std_logic;
        o_svnr_cpu_en   	: out  std_logic
    );
end entity svnr_en_controller;

architecture Behavioral of svnr_en_controller is
	type STATE is (
		Z_RUNNING,				--! 
		Z_WAIT_FOR_FINISHED_STEP,
		Z_OFF,  			--! 
		Z_HALTED,				--! 
		Z_STARTING				--! 
	);
	signal z_state: STATE := Z_OFF;
	signal svnr_en		: std_logic := '1';

begin
	svnr_en <= (i_cm_run and i_bp_run);

	process (i_clk)
	begin
		if rising_edge(i_clk) then
				
			CASE z_state is
				WHEN Z_RUNNING =>
					if (svnr_en = '0' or i_single_step_en = '1') and i_step_finished = '0' then
						z_state <= Z_WAIT_FOR_FINISHED_STEP;
					end if;
					if (i_bp_run = '0' or i_single_step_en = '1') and i_step_finished = '1' then
						o_svnr_cpu_en <= '0';
						z_state <= Z_HALTED;
					end if;

				WHEN Z_WAIT_FOR_FINISHED_STEP =>
					if i_step_finished = '1' then
						o_svnr_cpu_en <= '0';
						z_state <= Z_HALTED;
					end if;
					
				WHEN Z_HALTED => 
					if i_single_step_en = '0' then
						z_state <= Z_OFF;
					end if;

				WHEN Z_OFF =>
					o_svnr_cpu_en <= '0';
					if i_cm_run = '1' then
						o_svnr_cpu_en <= '1';
						z_state <= Z_STARTING;
					end if;
					
				WHEN Z_STARTING =>
					z_state <= Z_RUNNING;


				WHEN OTHERS =>
					z_state <= Z_OFF;
			END CASE;

		end if;
	end process;

end Behavioral;